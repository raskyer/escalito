using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Singleton;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class Customer : MonoBehaviour
{
    private const int AnimIdle = 0;
    private const int AnimMove = 1;
    private const float Speed = 30f;
    private const float Patience = 10f;
    private readonly List<Func<Cocktail, Cocktail, int>> _rules = new List<Func<Cocktail, Cocktail, int>>();
    private readonly HashSet<State> _states = new HashSet<State> {State.Idle};

    private Animator _animator;
    private Vector2 _dst;
    private float _minDistance;
    private Order _order;
    private int _satisfaction;
    private int _servedCount;
    private SpriteRenderer _spriteRenderer;
    private float _timeAwaited;
    private float _currentPatience;

    [SerializeField] private Text cashText;
    [SerializeField] private Image orderImage;
    [SerializeField] private Image waitingImage;
    [SerializeField] private Slider waitingSlider;

    public Func<Order> OrderBuilder { private get; set; }
    public float Offset => _spriteRenderer.sprite.bounds.extents.x;
    public bool HasOrder => _order != null;


    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rules.Add(Rules.CocktailRule);

        cashText.gameObject.SetActive(false);
        orderImage.gameObject.SetActive(false);
        waitingSlider.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_states.Contains(State.Wait))
        {
            StepWait();
        }

        if (_states.Contains(State.Idle))
        {
            StepIdle();
        }

        if (_states.Contains(State.Move))
        {
            StepMove();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!HasOrder)
        {
            return;
        }

        var glass = collision.gameObject.GetComponent<GlassSprite>();

        if (glass == null || glass.Served)
        {
            return;
        }

        glass.Served = true;
        Controller.Main.ReceiveOrder(this, glass);
    }

    private Vector2 Normalize(Vector2 dst, float offset)
    {
        return new Vector2(dst.x + offset, transform.position.y);
    }

    public bool IsNear(Vector2 dst, float offset, float minDistance)
    {
        return Vector2.Distance(transform.position, Normalize(dst, offset)) <= minDistance;
    }

    public void MoveTo(Vector2 dst, float offset, float minDistance)
    {
        if (dst == _dst || IsNear(dst, offset, minDistance))
        {
            return;
        }

        _dst = Normalize(dst, offset);
        _minDistance = minDistance;
        _states.Remove(State.Idle);
        _states.Add(State.Move);
        _spriteRenderer.flipX = _dst.x < transform.position.x;
    }

    public void AskOrder()
    {
        if (_order != null)
        {
            throw new InvalidOperationException("Customer has already order");
        }

        _order = OrderBuilder();
        orderImage.sprite = CocktailManager.Main.GetSprite(_order.Cocktail.Key);
        orderImage.gameObject.SetActive(true);
    }

    public void Await(int difficulty)
    {
        if (_states.Contains(State.Wait))
        {
            throw new InvalidOperationException("Customer is already awaiting");
        }

        _states.Add(State.Wait);
        _currentPatience = Patience / difficulty;
        _timeAwaited = 0;

        waitingSlider.gameObject.SetActive(true);
        waitingSlider.minValue = 0;
        waitingSlider.maxValue = _currentPatience;
    }

    public bool IsExhausted()
    {
        return _states.Contains(State.Exhausted);
    }

    private int Try(Cocktail expected, Cocktail actual)
    {
        return _rules.Sum(rule => rule(expected, actual)) / _rules.Count;
    }

    public void Serve(Cocktail actual)
    {
        if (!_states.Contains(State.Wait))
        {
            throw new InvalidOperationException("Customer is not awaiting");
        }

        _states.Remove(State.Wait);
        _timeAwaited = 0;
        _satisfaction = Try(_order.Cocktail, actual);

        orderImage.gameObject.SetActive(false);
        waitingSlider.gameObject.SetActive(false);
    }

    public bool IsSatisfied()
    {
        return _satisfaction > SatisfactionHelper.Low;
    }

    public int Pay()
    {
        if (!IsSatisfied())
        {
            return 0;
        }

        var bonus = _satisfaction > SatisfactionHelper.High && Random.Range(0, 4) == 0;
        var price = _order.Cocktail.Price + (bonus ? Random.Range(1, 5) : 0);

        cashText.gameObject.SetActive(true);
        cashText.text = "+" + price + "$";
        cashText.color = SatisfactionHelper.GetColor(_satisfaction);

        return price;
    }

    public void LeaveTo(Vector2 dst)
    {
        _spriteRenderer.color = SatisfactionHelper.GetColor(_satisfaction);
        orderImage.gameObject.SetActive(false);
        waitingSlider.gameObject.SetActive(false);
        MoveTo(dst, 0.0f, 0.0f);
    }

    private void StepWait()
    {
        _timeAwaited += Time.deltaTime;
        waitingSlider.value = _currentPatience - _timeAwaited;

        var percent = 100 - _timeAwaited / _currentPatience * 100;
        waitingImage.color = SatisfactionHelper.GetColor((int) percent);

        if (_timeAwaited < _currentPatience)
        {
            return;
        }

        _states.Remove(State.Wait);
        _states.Add(State.Exhausted);
    }

    private void StepIdle()
    {
        _animator.SetInteger("State", AnimIdle);
    }

    private void StepMove()
    {
        if (Vector2.Distance(transform.position, _dst) > _minDistance)
        {
            _animator.SetInteger("State", AnimMove);
            transform.position = Vector2.MoveTowards(transform.position, _dst, Speed * Time.deltaTime);
        }
        else
        {
            _animator.SetInteger("State", AnimIdle);
            _states.Remove(State.Move);
            _states.Add(State.Idle);
            _dst = Vector2.zero;
            _minDistance = 0;
        }
    }

    private enum State
    {
        Idle,
        Move,
        Wait,
        Exhausted
    }
}