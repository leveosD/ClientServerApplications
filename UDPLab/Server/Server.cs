using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
            if(!existingBag.Contains(clientId))
                existingBag.Add(clientId);
            return existingBag;
        });

        _clientTopics.AddOrUpdate(clientId, new ConcurrentBag<string> { topic }, (key, existingBag) =>
        {
            if(!existingBag.Contains(topic))
                existingBag.Add(topic);
            return existingBag;
        });

        Console.WriteLine($"Client {clientId} subscribed to {topic}");

        var parentTopics = GetParentTopics(topic);
        foreach (var parentTopic in parentTopics)
        {
            Subscribe(clientId, parentTopic);
        }
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
        Console.WriteLine($"Client {clientId} unsubscribed from {topic}");
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

public class Server
{
    private Socket _udpSocket;
    private readonly int _port;
    private readonly SubscriptionManager _subscriptionManager;
    private readonly ConcurrentDictionary<string, IPEndPoint> _connectedClients = new ConcurrentDictionary<string, IPEndPoint>();
    private int _clientCounter = 0;
    private Regex _pattern = new Regex(@"^([a-z]{3})(\.[a-z]{3}){0,2}$");

    public Server(int port)
    {
        _port = port;
        _subscriptionManager = new SubscriptionManager();
    }

    public void Start()
    {
        try
        {
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
            Console.WriteLine($"Server started on port {_port}");

            ListenForClients();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting server: {ex.Message}");
        }
    }

    private void ListenForClients()
    {
        var buffer = new byte[8192];
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
        while (true)
        {
            try
            {
                int receivedBytes = _udpSocket.ReceiveFrom(buffer, ref remoteEndPoint);
                var clientId = $"Client_{Interlocked.Increment(ref _clientCounter)}";

                _connectedClients.TryAdd(clientId, (IPEndPoint)remoteEndPoint);
                Console.WriteLine($"Client connected: {clientId}");

                var message = DeserializeMessage(buffer, receivedBytes);
                HandleClientMessage(clientId, message, (IPEndPoint)remoteEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving data: {ex.Message}");
            }
        }
    }

    public Message DeserializeMessage(byte[] messageBuffer, int length)
    {
        var jsonMessage = Encoding.UTF8.GetString(messageBuffer, 0, length);
        using (JsonDocument doc = JsonDocument.Parse(jsonMessage))
        {
            string type = doc.RootElement.GetProperty("Type").GetString();

            return type switch
            {
                "AuthorizationRequest" => JsonSerializer.Deserialize<AuthorizationRequest>(jsonMessage),
                "AuthorizationResponse" => JsonSerializer.Deserialize<AuthorizationResponse>(jsonMessage),
                "TopicMessage" => JsonSerializer.Deserialize<TopicMessage>(jsonMessage),
                "SubscribeRequest" => JsonSerializer.Deserialize<SubscribeRequest>(jsonMessage),
                "UnsubscribeRequest" => JsonSerializer.Deserialize<UnsubscribeRequest>(jsonMessage),
                "MessageResponse" => JsonSerializer.Deserialize<MessageResponse>(jsonMessage),
                _ => throw new NotSupportedException($"Type {type} не поддерживается."),
            };
        }
    }

    private void SendJsonViaSocket(IPEndPoint endPoint, object message)
    {
        var jsonMessage = JsonSerializer.Serialize(message);
        byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
        Console.WriteLine($"[SERVER SEND] JSON: {jsonMessage}");
        _udpSocket.SendTo(data, endPoint);
    }

    private void HandleClientMessage(string clientId, Message message, IPEndPoint clientEndPoint)
    {
        try
        {
            if (message is AuthorizationRequest authRequest)
            {
                bool isAuthorized = (authRequest.Username == "user" && authRequest.Password == "password");
                var authResponse = new AuthorizationResponse { IsAuthorized = isAuthorized, Token = Guid.NewGuid().ToString() };
                SendJsonViaSocket(clientEndPoint, authResponse);

                if (!isAuthorized)
                {
                    Console.WriteLine($"Client {clientId} failed authorization.");
                    return;
                }
                
                Console.WriteLine($"Client {clientId} authorized.");
                // Register the client.
                _connectedClients[clientId] = clientEndPoint;
            }
            else if (message is SubscribeRequest subRequest)
            {
                if(_pattern.IsMatch(subRequest.Topic))
                    _subscriptionManager.Subscribe(clientId, subRequest.Topic);
                else
                {
                    var errorResponse = new MessageResponse
                        { Success = false, ErrorMessage = "Topic is not fitted to the pattern." };
                    SendJsonViaSocket(clientEndPoint, errorResponse);
                }
            }
            else if (message is UnsubscribeRequest unsubRequest)
            {
                if(_pattern.IsMatch(unsubRequest.Topic))
                    _subscriptionManager.Unsubscribe(clientId, unsubRequest.Topic);
                else
                {
                    var errorResponse = new MessageResponse
                        { Success = false, ErrorMessage = "Topic is not fitted to the pattern." };
                    SendJsonViaSocket(clientEndPoint, errorResponse);
                }
            }
            else if (message is TopicMessage topicMessage)
            {
                var subscribers = _subscriptionManager.GetSubscribers(topicMessage.Topic);
                foreach (var subscriberId in subscribers)
                {
                    if (_connectedClients.TryGetValue(subscriberId, out var subscriberEndPoint))
                    {
                        SendJsonViaSocket(subscriberEndPoint, topicMessage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling message from client {clientId}: {ex.Message}");
        }
    }
}