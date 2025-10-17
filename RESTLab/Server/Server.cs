using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServerNamespace
{
    public class Server
    {
        private HttpListener _listener;
        private readonly int _port;
        private readonly SubscriptionManager _subscriptionManager;
        private Dictionary<string, MessageQueue> _messageQueues;
        private Regex _pattern = new Regex(@"^([a-z]{3})(\.[a-z]{3}){0,2}$");

        public Server(int port)
        {
            _port = port;
            _subscriptionManager = new SubscriptionManager();
            _messageQueues = new Dictionary<string, MessageQueue>();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"Сервер запущен на http://localhost:{_port}/");
            while (true)
            {
                var context = _listener.GetContext();
                HandleRequest(context);
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var response = context.Response;
            var request = context.Request;

            try
            {
                if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/authorize")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        var json = reader.ReadToEnd();
                        var authRequest = JsonSerializer.Deserialize<AuthorizationRequest>(json);
                        bool isAuthorized = (authRequest.Username == "user" && "password" == authRequest.Password);

                        var authResponse = new AuthorizationResponse
                        {
                            IsAuthorized = isAuthorized,
                            Token = isAuthorized ? Guid.NewGuid().ToString() : null
                        };
                        _messageQueues.Add(authResponse.Token, new MessageQueue());
                        response.StatusCode = isAuthorized ? (int)HttpStatusCode.OK : (int)HttpStatusCode.Unauthorized;
                        SendResponse(response, authResponse);
                    }
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/subscribe")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        var json = reader.ReadToEnd();
                        var data = JsonSerializer.Deserialize<SubscribeRequest>(json);
                        MessageResponse responseMessage;
                        if (_pattern.IsMatch(data.Topic))
                        {
                            _subscriptionManager.Subscribe(data.Token, data.Topic);
                            response.StatusCode = (int)HttpStatusCode.OK;
                            responseMessage = new MessageResponse { Success = true };
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            responseMessage = new MessageResponse
                                { Success = false, ErrorMessage = "Topic is not fitted to the pattern." };
                        }

                        SendResponse(response, responseMessage);
                    }
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/unsubscribe")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        var json = reader.ReadToEnd();
                        var data = JsonSerializer.Deserialize<UnsubscribeRequest>(json);
                        _subscriptionManager.Unsubscribe(data.Token, data.Topic);
                        response.StatusCode = (int)HttpStatusCode.OK;
                        var responseMessage = new MessageResponse { Success = true };
                        SendResponse(response, responseMessage);
                    }
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/send")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        var json = reader.ReadToEnd();
                        var data = JsonSerializer.Deserialize<TopicMessage>(json);

                        foreach (var client in _messageQueues)
                        {
                            client.Value.EnqueueMessage(data.Topic, data);
                            var parents = _subscriptionManager.GetParentTopics(data.Topic);
                            foreach (var p in parents)
                            {
                                client.Value.EnqueueMessage(data.Topic, data);
                            }
                        }

                        response.StatusCode = (int)HttpStatusCode.OK;
                        var responseMessage = new MessageResponse { Success = true };
                        SendResponse(response, responseMessage);
                    }
                }
                else if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/new_messages")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        var json = reader.ReadToEnd();
                        var data = JsonSerializer.Deserialize<MessagesRequest>(json);

                        var topics = _subscriptionManager.GetClientTopics(data.Token);

                        var responseMessages = new List<TopicMessage>();
                        foreach (var topic in topics)
                        {
                            var messages = _messageQueues[data.Token].DequeueMessages(topic);
                            foreach (var mes in messages)
                            {
                                responseMessages.Add(mes);
                            }
                        }

                        response.StatusCode = (int)HttpStatusCode.OK;
                        SendResponse(response, responseMessages);
                    }
                }
                else if (request.HttpMethod == "GET" && request.Url.AbsolutePath.StartsWith("/subscriptions"))
                {
                    var client = request.Url.AbsolutePath.Substring("/subscriptions/".Length);
                    var subscribers = _subscriptionManager.GetClientTopics(client);
                    response.StatusCode = (int)HttpStatusCode.OK;
                    SendResponse(response, subscribers);
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e);
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            finally
            {
                response.Close();
            }
        }

        private void SendResponse(HttpListenerResponse response, object responseObject)
        {
            var jsonResponse = JsonSerializer.Serialize(responseObject);
            var buffer = Encoding.UTF8.GetBytes(jsonResponse);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "application/json";
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
    }
}