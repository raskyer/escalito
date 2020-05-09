﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cocktails;
using Components;
using Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Characters
{
    [Serializable]
    public class CustomerEntry
    {
        public CharacterKey key;
        public GameObject prefab;
    }

    public class CharacterController : Controller
    {
        private const int MinDistance = 2;
        private const int MaxDistance = 3;
        private const int ReputationThreshold = 10;

        public static CharacterController Main;
        private readonly Queue<Customer> _customersLeave = new Queue<Customer>();
        private readonly LinkedList<Customer> _customersQueue = new LinkedList<Customer>();

        private readonly Dictionary<CharacterKey, GameObject> _prefabs = new Dictionary<CharacterKey, GameObject>();
        private readonly TimingAction _customerSpawnAction;
        private readonly TimingAction _sponsorSpawnAction;
        private readonly Vector2 _spawnRange = new Vector2(2, 5);
        private int _customerLimit = 3;
        private Glass _glass;
        public Transform bar;

        public List<CustomerEntry> entries;
        public Transform spawn;

        public CharacterController()
        {
            _customerSpawnAction = new TimingAction(0, SpawnCustomerCondition, SpawnCustomerTrigger);
            _sponsorSpawnAction = new TimingAction(1, SpawnSponsorCondition, SpawnSponsorTrigger);
        }

        private void Awake()
        {
            Main = this;
            foreach (var spawnEntry in entries) _prefabs.Add(spawnEntry.key, spawnEntry.prefab);
        }

        private void Update()
        {
            UpdateQueue();
            UpdateLeaving();
            UpdateSpawn();
        }

        private void UpdateQueue()
        {
            for (var node = _customersQueue.Last; node != null;)
            {
                var customer = node.Value;

                if (node.Previous == null)
                {
                    GoToBar(customer);
                }
                else
                {
                    GoToCustomer(customer, node.Previous.Value);
                }

                if (customer.Exhausted)
                {
                    Leave(customer);
                }

                if (!customer.HasOrder && customer.IsNear(bar.position, -customer.Offset, MaxDistance))
                {
                    AskOrder(customer);
                }

                node = node.Previous;
            }
        }

        private void UpdateLeaving()
        {
            while (_customersLeave.Count > 0)
            {
                var leavingCustomer = _customersLeave.Peek();
                if (leavingCustomer.IsNear(spawn.position, 0, MinDistance))
                {
                    Destroy(_customersLeave.Dequeue().gameObject);
                }
                else
                {
                    break;
                }
            }
        }

        private void UpdateSpawn()
        {
            _customerSpawnAction.Tick(Time.deltaTime);
            _sponsorSpawnAction.Tick(Time.deltaTime);
        }

        private void GoToBar(Character customer)
        {
            customer.MoveTo(bar.position, -customer.Offset, MinDistance);
        }

        private void GoToCustomer(Character customer, Character leader)
        {
            customer.MoveTo(leader.transform.position, -customer.Offset, MinDistance);
        }

        private void AskOrder(Customer customer)
        {
            customer.AskOrder();
            customer.Await(MainController.Main.Difficulty);
            _glass = GlassController.Main.Spawn();
        }

        public void ReceiveOrder(Customer customer, Glass glass)
        {
            var actual = glass.Cocktail;
            Destroy(glass.gameObject);

            var cash = customer.Serve(actual);

            if (customer.Satisfied)
            {
                MainController.Main.IncrementSuccess(customer, cash);
            }
            else
            {
                MainController.Main.IncrementFailure(customer);
            }

            Leave(customer);
        }

        private void Leave(Customer customer)
        {
            var node = _customersQueue.Find(customer);

            if (node == null)
            {
                throw new InvalidOperationException("Customer couldn't be found in customers queue");
            }

            if (customer.Satisfied)
            {
                AudioController.Main.success.Play();
            }
            else
            {
                AudioController.Main.failure.Play();
            }

            _customersQueue.Remove(node);
            _customersLeave.Enqueue(customer);
            customer.LeaveTo(spawn.position);
            Destroy(_glass.gameObject);
            _glass = null;
        }
        
        private async void SponsorBehaviour(Sponsor sponsor)
        {
            sponsor.noButton.onClick.AddListener(() => RefuseContract(sponsor));
            sponsor.yesButton.onClick.AddListener( () => AcceptContract(sponsor));

            if (!await sponsor.MoveToAsync(bar.position))
            {
                return;
            }

            sponsor.AskContract();
            await Task.Delay(5000);
            sponsor.RefuseContract();
            Leave(sponsor);
        }

        private void RefuseContract(Sponsor sponsor)
        {
            sponsor.RefuseContract();
            Leave(sponsor);
        }

        private void AcceptContract(Sponsor sponsor)
        {
            sponsor.AcceptContract();
            Leave(sponsor);
        }

        private async void Leave(Sponsor sponsor)
        {
            if (sponsor.Leaving || !await sponsor.LeaveToAsync(spawn.position))
            {
                return;
            }
            Destroy(sponsor.gameObject);
        }

        private bool SpawnCustomerCondition()
        {
            return MainController.Main.BarIsOpen && _customersQueue.Count <= _customerLimit;
        }

        private float SpawnCustomerTrigger()
        {
            var customer = SpawnRandomCustomer();
            _customersQueue.AddLast(customer);
            return Random.Range(_spawnRange.x, _spawnRange.y);
        }

        private bool SpawnSponsorCondition()
        {
            return MainController.Main.Reputation > ReputationThreshold;
        }

        private float SpawnSponsorTrigger()
        {
            var sponsor = SpawnCharacter<Sponsor>(CharacterKey.Sponsor);

            var t = sponsor.transform;
            var p = t.position;
            t.position = new Vector3(p.x, p.y + 5, p.z);

            SponsorBehaviour(sponsor);
            
            return 100;
        }

        private Customer SpawnRandomCustomer()
        {
            var keys = Enum.GetValues(typeof(CharacterKey))
                .Cast<CharacterKey>()
                .Where(c => c != CharacterKey.Sponsor)
                .ToArray();
            var rand = Random.Range(0, keys.Length);
            var key = keys[rand];

            var customer = SpawnCharacter<Customer>(key);
            customer.OrderBuilder = Order.BuildRandom;

            return customer;
        }

        private T SpawnCharacter<T>(CharacterKey key) where T : MonoBehaviour
        {
            if (!_prefabs.TryGetValue(key, out var prefab))
            {
                throw new InvalidOperationException();
            }

            return CreateComponent<T>(prefab, spawn, prefab.name);
        }
    }
}