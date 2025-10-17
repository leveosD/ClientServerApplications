using System.Collections.Concurrent;

namespace ServerNamespace
{
    public class SubscriptionManager
    {
        private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _subscriptions =
            new ConcurrentDictionary<string, ConcurrentBag<string>>();

        private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _clientTopics =
            new ConcurrentDictionary<string, ConcurrentBag<string>>();

        public void Subscribe(string clientId, string topic)
        {
            _subscriptions.AddOrUpdate(topic, new ConcurrentBag<string> { clientId }, (key, existingBag) =>
            {
                if (!existingBag.Contains(clientId))
                    existingBag.Add(clientId);
                return existingBag;
            });

            _clientTopics.AddOrUpdate(clientId, new ConcurrentBag<string> { topic }, (key, existingBag) =>
            {
                if (!existingBag.Contains(topic))
                    existingBag.Add(topic);
                return existingBag;
            });

            Console.WriteLine($"Client {clientId} subscribed to {topic}");
        }

        public void Unsubscribe(string clientId, string topic)
        {
            if (_subscriptions.TryGetValue(topic, out var clients))
            {
                clients.TryTake(out _);
                if (clients.IsEmpty)
                {
                    _subscriptions.TryRemove(topic, out _);
                }
            }

            if (_clientTopics.TryGetValue(clientId, out var topics))
            {
                topics.TryTake(out _);
                if (topics.IsEmpty)
                {
                    _clientTopics.TryRemove(clientId, out _);
                }
            }
        }

        public List<string> GetSubscribers(string topic)
        {
            var parentTopics = GetParentTopics(topic);

            var subscribers = new List<string>();
            if (_subscriptions.TryGetValue(topic, out var clients))
            {
                subscribers.AddRange(clients);
            }

            foreach (var pTopic in parentTopics)
            {
                if (_subscriptions.TryGetValue(pTopic, out clients))
                {
                    subscribers.AddRange(clients);
                }
            }

            return subscribers;
        }

        public List<string> GetClientTopics(string clientId)
        {
            if (_clientTopics.TryGetValue(clientId, out var topics))
            {
                return new List<string>(topics);
            }

            return new List<string>();
        }

        public List<string> GetParentTopics(string topic)
        {
            var parentTopics = new List<string>();
            var parts = topic.Split('.');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                parentTopics.Add(string.Join(".", parts.Take(i + 1)));
            }

            return parentTopics;
        }
    }
}