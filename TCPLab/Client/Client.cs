using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public class Client
{
    private readonly string _serverIp;
    private readonly int _serverPort;
    private Socket _clientSocket;
    private bool _isConnected = false;
    private string _clientId;
    private string _authApiUrl = "http://localhost:5000";

    public string token;

    public Client(string serverIp, int serverPort)
    {
        //_jsonSettings.Converters.Add(new MyConverter());
        _serverIp = serverIp;
        _serverPort = serverPort;
    }

    public bool Connect()
    {
        try
        {
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _clientSocket.Connect(_serverIp, _serverPort);
            _isConnected = true;
            _clientId = $"Client_{Guid.NewGuid().ToString().Substring(0, 4)}";

            Console.WriteLine($"Client connected to server on {_serverIp}:{_serverPort}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to server: {ex.Message}");
            _isConnected = false;
            return false;
        }
    }

    public Message DeserializeMessage(string jsonString)
    {
        using (JsonDocument doc = JsonDocument.Parse(jsonString))
        {
            // Проверяем значение свойства Type
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

    private void SendJsonViaSocket(object message)
    {
        if (!_isConnected)
        {
            return;
        }
        try
        {
            //var jsonMessage = JsonConvert.SerializeObject(message, _jsonSettings);
            var jsonMessage = JsonSerializer.Serialize(message);
            byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
            byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
            _clientSocket.Send(lengthPrefix);
            _clientSocket.Send(data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
            Disconnect();
        }
    }

    private object? ReceiveJsonViaSocket()
    {
        if (!_isConnected)
        {
            return null;
        }

        try
        {
            var buffer = new byte[4];
            int bytesRead = _clientSocket.Receive(buffer);
            if (bytesRead != 4) throw new Exception("Could not read length prefix.");

            int messageLength = BitConverter.ToInt32(buffer, 0);
            byte[] messageBuffer = new byte[messageLength];
            bytesRead = _clientSocket.Receive(messageBuffer);
            if (bytesRead != messageLength) throw new Exception("Could not read full message.");

            var jsonMessage = Encoding.UTF8.GetString(messageBuffer);
            return DeserializeMessage(jsonMessage);
        }
        catch (SocketException se) when (se.SocketErrorCode == SocketError.ConnectionReset ||
                                         se.SocketErrorCode == SocketError.Interrupted)
        {
            Console.WriteLine($"Socket connection lost or interrupted: {se.Message}");
            Disconnect();
            return null;
        }
        catch (SocketException se) when (se.SocketErrorCode == SocketError.ConnectionAborted)
        {
            Disconnect();
            return null;
        }
        catch (JsonException je)
        {
            Console.WriteLine($"Json parsing error: {je.Message}");
            return null;
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"Error we: {ex.Message}");
            Disconnect();
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error receiving message: {ex.Message}");
            Disconnect();
            return null;
        }
    }

    public bool Authorize(string username, string password)
    {
        if (!_isConnected)
        {
            return false;
        }
        var authRequest = new AuthorizationRequest { Username = username, Password = password };
        SendJsonViaSocket(authRequest);

        var authResponse = (AuthorizationResponse)ReceiveJsonViaSocket();

        if (authResponse != null && authResponse.IsAuthorized)
        {
            Console.WriteLine("Authorization successful. Token: " + authResponse.Token);
            token = authResponse.Token;
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
                    object? receivedObj = ReceiveJsonViaSocket();
                    if (receivedObj == null) break;

                    if (receivedObj is TopicMessage topicMessage)
                    {
                        Console.WriteLine($"Received message on topic '{topicMessage.Topic}': {topicMessage.Payload}");
                    }
                    else if (receivedObj is MessageResponse messageResponse)
                    {
                        Console.WriteLine($"Received message: {messageResponse.ErrorMessage}");
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
        })
        { IsBackground = true }.Start();
    }

    public void Disconnect()
    {
        if (_isConnected)
        {
            _isConnected = false;
            try
            {
                if (_clientSocket != null && _clientSocket.Connected)
                {
                    _clientSocket.Shutdown(SocketShutdown.Both);
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