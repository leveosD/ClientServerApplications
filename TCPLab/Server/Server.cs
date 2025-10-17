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

        /*var parentTopics = GetParentTopics(topic);
        foreach (var parentTopic in parentTopics)
        {
            Subscribe(clientId, parentTopic);
        }*/
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
    private Socket _listenerSocket;
    private readonly int _port;
    private readonly SubscriptionManager _subscriptionManager;
    private readonly Dictionary<string, Socket> _connectedClients = new Dictionary<string, Socket>();
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
            _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
            _listenerSocket.Listen(100);
            Console.WriteLine($"Server started on port {_port}");

            new Thread(AcceptConnections).Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting server: {ex.Message}");
        }
    }

    private void AcceptConnections()
    {
        while (true)
        {
            try
            {
                Socket clientSocket = _listenerSocket.Accept();
                var clientId = $"Client_{Interlocked.Increment(ref _clientCounter)}";
                _connectedClients.Add(clientId, clientSocket);
                Console.WriteLine($"Client connected: {clientId}");
                new Thread(HandleClient).Start(new Tuple<string, Socket>(clientId, clientSocket));
            }
            catch (SocketException se)
            {
                Console.WriteLine($"SocketException in AcceptConnections: {se.Message}");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting connections: {ex.Message}");
            }
        }
    }
    
    public Message DeserializeMessage(string jsonString)
    {
        using (JsonDocument doc = JsonDocument.Parse(jsonString))
        {
            string type = doc.RootElement.GetProperty("Type").GetString();

            return type switch
            {
                "AuthorizationRequest" => JsonSerializer.Deserialize<AuthorizationRequest>(jsonString),
                "AuthorizationResponse" => JsonSerializer.Deserialize<AuthorizationResponse>(jsonString),
                "TopicMessage" => JsonSerializer.Deserialize<TopicMessage>(jsonString),
                "SubscribeRequest" => JsonSerializer.Deserialize<SubscribeRequest>(jsonString),
                "UnsubscribeRequest" => JsonSerializer.Deserialize<UnsubscribeRequest>(jsonString),
                "MessageResponse" => JsonSerializer.Deserialize<MessageResponse>(jsonString),
                _ => throw new NotSupportedException($"Type {type} не поддерживается."),
            };
        }
    }

    private void SendJsonViaSocket(Socket socket, object message)
    {
        var jsonMessage = JsonSerializer.Serialize(message);
        byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
        byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
        Console.WriteLine($"[SERVER SEND] Length: {data.Length}, JSON: {jsonMessage}");
        socket.Send(lengthPrefix);
        socket.Send(data);
    }

    private object ReceiveJsonViaSocket(Socket socket)
    {
        var buffer = new byte[4];
        int bytesRead = socket.Receive(buffer);
        if (bytesRead != 4) throw new Exception($"Could not read length prefix. {bytesRead}");

        int messageLength = BitConverter.ToInt32(buffer, 0);
        byte[] messageBuffer = new byte[messageLength];
        bytesRead = socket.Receive(messageBuffer);
        if (bytesRead != messageLength) throw new Exception("Could not read full message.");

        string? jsonMessage; 
        try
        {
            jsonMessage = Encoding.UTF8.GetString(messageBuffer);
        }
        catch (Exception e)
        {
            Console.WriteLine("JSON error: " + e);
            throw;
        }
        return DeserializeMessage(jsonMessage);
    }

    private void HandleClient(object obj)
    {
        var clientInfo = (Tuple<string, Socket>)obj;
        var clientId = clientInfo.Item1;
        var clientSocket = clientInfo.Item2;

        try
        {
            var authRequest = (AuthorizationRequest)ReceiveJsonViaSocket(clientSocket);
            bool isAuthorized = (authRequest.Username == "user" && authRequest.Password == "password");
            var authResponse = new AuthorizationResponse { IsAuthorized = isAuthorized, Token = Guid.NewGuid().ToString() };
            SendJsonViaSocket(clientSocket, authResponse);

            if (!isAuthorized)
            {
                Console.WriteLine($"Client {clientId} failed authorization.");
                clientSocket.Close();
                _connectedClients.Remove(clientId);
                return;
            }
            Console.WriteLine($"Client {clientId} authorized.");

            while (true)
            {
                object receivedObj = ReceiveJsonViaSocket(clientSocket);

                if (receivedObj is SubscribeRequest subRequest)
                {
                    if(_pattern.IsMatch(subRequest.Topic))
                        _subscriptionManager.Subscribe(clientId, subRequest.Topic);
                    else
                    {
                        var errorResponse = new MessageResponse
                            { Success = false, ErrorMessage = "Topic is not fitted to the pattern." };
                        SendJsonViaSocket(clientSocket, errorResponse);
                    }
                }
                else if (receivedObj is UnsubscribeRequest unsubRequest)
                {
                    if(_pattern.IsMatch(unsubRequest.Topic))
                       _subscriptionManager.Unsubscribe(clientId, unsubRequest.Topic);
                    else
                    {
                        var errorResponse = new MessageResponse
                            { Success = false, ErrorMessage = "Topic is not fitted to the pattern." };
                        SendJsonViaSocket(clientSocket, errorResponse);
                    }
                }
                else if (receivedObj is TopicMessage topicMessage)
                {
                    Console.WriteLine($"Received message from {clientId} on topic {topicMessage.Topic}");

                    var subscribers = _subscriptionManager.GetSubscribers(topicMessage.Topic);
                    foreach (var subscriberId in subscribers)
                    {
                        if (_connectedClients.TryGetValue(subscriberId, out var subscriberSocket) && subscriberSocket.Connected)
                        {
                            try
                            {
                                SendJsonViaSocket(subscriberSocket, topicMessage);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error sending message to {subscriberId}: {ex.Message}");
                                _connectedClients.Remove(subscriberId);
                                subscriberSocket.Close();
                            }
                        }
                    }
                }
            }
        }
        catch (SocketException se)
        {
            Console.WriteLine($"SocketException with client {clientId}: {se.Message}");
        }
        catch (JsonException je)
        {
            Console.WriteLine($"JsonException with client {clientId}: {je.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client {clientId}: {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"Client {clientId} disconnected.");
            _connectedClients.Remove(clientId);
            clientSocket.Close();

            var topics = _subscriptionManager.GetClientTopics(clientId);
            foreach (var topic in topics)
            {
                _subscriptionManager.Unsubscribe(clientId, topic);
            }
        }
    }
}