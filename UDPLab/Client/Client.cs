using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class Client
{
    private readonly string _serverIp;
    private readonly int _serverPort;
    private readonly IPEndPoint _serverEndPoint;
    private Socket _clientSocket;
    private bool _isConnected = false;
    private string _clientId;
    private string _authApiUrl = "http://localhost:5000";

    public Client(string serverIp, int serverPort)
    {
        _serverIp = serverIp;
        _serverPort = serverPort;
        _serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
    }

    public bool Connect()
    {
        try
        {
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _clientId = $"Client_{Guid.NewGuid().ToString().Substring(0, 4)}";
            Console.WriteLine($"Client initialized, ready to communicate with {_serverIp}:{_serverPort}");
            _isConnected = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize client: {ex.Message}");
            _isConnected = false;
            return false;
        }
    }

    public Message? DeserializeMessage(byte[] messageBuffer, int messageLength)
    {
        var jsonMessage = Encoding.UTF8.GetString(messageBuffer, 0, messageLength);
        using (JsonDocument doc = JsonDocument.Parse(jsonMessage))
        {
            string? type = doc.RootElement.GetProperty("Type").GetString();

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

    private void SendJsonViaSocket(object message)
    {
        if (!_isConnected) return;
        
        try
        {
            var jsonMessage = JsonSerializer.Serialize(message);
            byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
            Console.WriteLine("Sending message: " + jsonMessage);
            _clientSocket.SendTo(data, _serverEndPoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
            Disconnect();
        }
    }

    private object ReceiveJsonViaSocket()
    {
        if (!_isConnected)
        {
            Console.WriteLine("Not connected");
            return null;
        }

        try
        {
            var buffer = new byte[8192];
            EndPoint remoteEndPoint = _serverEndPoint;
            int bytesRead = _clientSocket.ReceiveFrom(buffer, ref remoteEndPoint);
            Console.WriteLine("Received length of message: " + bytesRead);
            return DeserializeMessage(buffer, bytesRead);
        }
        catch (SocketException se) when (se.SocketErrorCode == SocketError.Interrupted)
        {
            Disconnect();
            return null;
        }
        catch (SocketException se)
        {
            Console.WriteLine($"Socket error: {se.Message}");
            Disconnect();
            return null;
        }
        catch (JsonException je)
        {
            Console.WriteLine($"Json parsing error: {je.Message}");
            return null;
        }
    }

    public bool Authorize(string username, string password)
    {
        var authRequest = new AuthorizationRequest { Username = username, Password = password };
        SendJsonViaSocket(authRequest);

        Console.WriteLine("Waiting for auth response...");
        var authResponse = (AuthorizationResponse)ReceiveJsonViaSocket();

        if (authResponse != null && authResponse.IsAuthorized)
        {
            Console.WriteLine("Authorization successful. Token: " + authResponse.Token);
            return true;
        }
        else
        {
            Console.WriteLine("Authorization failed.");
            Disconnect();
            return false;
        }
    }

    public void Subscribe(string topic)
    {
        var subRequest = new SubscribeRequest { Topic = topic };
        SendJsonViaSocket(subRequest);
        Console.WriteLine($"Subscribed to topic: {topic}");
    }

    public void Unsubscribe(string topic)
    {
        var unsubRequest = new UnsubscribeRequest { Topic = topic };
        SendJsonViaSocket(unsubRequest);
        Console.WriteLine($"Unsubscribed from topic: {topic}");
    }

    public void SendMessage(string topic, object payload)
    {
        var topicMessage = new TopicMessage { Topic = topic, Payload = payload };
        SendJsonViaSocket(topicMessage);
        Console.WriteLine($"Sent message to topic {topic}");
    }

    public void ListenForMessages()
    {
        new Thread(() =>
        {
            try
            {
                while (_isConnected)
                {
                    object receivedObj = ReceiveJsonViaSocket();
                    if (receivedObj == null) break;

                    if (receivedObj is TopicMessage topicMessage)
                    {
                        Console.WriteLine($"Received message on topic '{topicMessage.Topic}': {topicMessage.Payload}");
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Console.WriteLine("Listener thread aborted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in listener thread: {ex.Message}");
            }
            finally
            {
                if (_isConnected) Disconnect();
            }
        }) { IsBackground = true }.Start();
    }

    public void Disconnect()
    {
        if (_isConnected)
        {
            _isConnected = false;
            try
            {
                if (_clientSocket != null)
                {
                    _clientSocket.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing socket: {ex.Message}");
            }
            finally
            {
                _clientSocket = null;
            }
            Console.WriteLine("Client disconnected.");
        }
    }
}