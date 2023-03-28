﻿using System.Collections.Immutable;

namespace WebSocketDemo;

internal class AsyncEvent<T>
    where T : class
{
    private readonly object _subLock = new();
    private ImmutableArray<T> _subscriptions;

    public bool HasSubscribers => _subscriptions.Length != 0;
    public IReadOnlyList<T> Subscriptions => _subscriptions;

    public AsyncEvent()
    {
        lock (_subLock)
        {
            _subscriptions = ImmutableArray.Create<T>();
        }
    }

    public void Add(T subscriber)
    {
        if (subscriber is null) throw new ArgumentNullException(nameof(subscriber));
        lock (_subLock)
        {
            _subscriptions = _subscriptions.Add(subscriber);
        }
    }

    public void Remove(T subscriber)
    {
        if (subscriber is null) throw new ArgumentNullException(nameof(subscriber));
        lock (_subLock)
        {
            _subscriptions = _subscriptions.Remove(subscriber);
        }
    }
}

internal static class EventExtensions
{
    public static async Task InvokeAsync(this AsyncEvent<Func<Task>> eventHandler)
    {
        IReadOnlyList<Func<Task>> subscribers = eventHandler.Subscriptions;
        for (int i = 0; i < subscribers.Count; i++) await subscribers[i].Invoke().ConfigureAwait(false);
    }

    public static async Task InvokeAsync<T>(this AsyncEvent<Func<T, Task>> eventHandler, T arg)
    {
        IReadOnlyList<Func<T, Task>> subscribers = eventHandler.Subscriptions;
        for (int i = 0; i < subscribers.Count; i++) await subscribers[i].Invoke(arg).ConfigureAwait(false);
    }

    public static async Task InvokeAsync<T1, T2>(this AsyncEvent<Func<T1, T2, Task>> eventHandler, T1 arg1, T2 arg2)
    {
        IReadOnlyList<Func<T1, T2, Task>> subscribers = eventHandler.Subscriptions;
        for (int i = 0; i < subscribers.Count; i++) await subscribers[i].Invoke(arg1, arg2).ConfigureAwait(false);
    }

    public static async Task InvokeAsync<T1, T2, T3>(this AsyncEvent<Func<T1, T2, T3, Task>> eventHandler, T1 arg1, T2 arg2, T3 arg3)
    {
        IReadOnlyList<Func<T1, T2, T3, Task>> subscribers = eventHandler.Subscriptions;
        for (int i = 0; i < subscribers.Count; i++) await subscribers[i].Invoke(arg1, arg2, arg3).ConfigureAwait(false);
    }

    public static async Task InvokeAsync<T1, T2, T3, T4>(this AsyncEvent<Func<T1, T2, T3, T4, Task>> eventHandler, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        IReadOnlyList<Func<T1, T2, T3, T4, Task>> subscribers = eventHandler.Subscriptions;
        for (int i = 0; i < subscribers.Count; i++) await subscribers[i].Invoke(arg1, arg2, arg3, arg4).ConfigureAwait(false);
    }

    public static async Task InvokeAsync<T1, T2, T3, T4, T5>(this AsyncEvent<Func<T1, T2, T3, T4, T5, Task>> eventHandler, T1 arg1, T2 arg2, T3 arg3,
        T4 arg4, T5 arg5)
    {
        IReadOnlyList<Func<T1, T2, T3, T4, T5, Task>> subscribers = eventHandler.Subscriptions;
        for (int i = 0; i < subscribers.Count; i++) await subscribers[i].Invoke(arg1, arg2, arg3, arg4, arg5).ConfigureAwait(false);
    }
}