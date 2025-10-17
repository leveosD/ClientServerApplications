using System.Text;
using System.Text.Json;

public class Client
{
    private readonly string _serverUrl;
    private readonly HttpClient _httpClient;
    private string _token;

    public Client(string serverUrl)
    {
        _serverUrl = serverUrl;
        _httpClient = new HttpClient();
    }

    public bool Authorize(string username, string password)
    {
        var authRequest = new AuthorizationRequest
        {
            Username = username,
            Password = password,
        };

        var jsonRequest
            = JsonSerializer.Serialize(authRequest);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        var response = _httpClient.PostAsync($"{_serverUrl}/authorize", content).Result;

        if (response.IsSuccessStatusCode)
        {
            var authResponse = JsonSerializer.Deserialize<AuthorizationResponse>(response.Content.ReadAsStringAsync().Result);
            if (authResponse != null && authResponse.IsAuthorized)
            {
                Console.WriteLine("Authorization successful. Token: " + authResponse.Token);
                _token = authResponse.Token;
                return true;
            }
        }

        Console.WriteLine("Authorization failed.");
        return false;
    }

    public void Subscribe(string topic)
    {
        var subRequest = new SubscribeRequest
        {
            Topic = topic,
            Token = _token
        };

        var jsonRequest = JsonSerializer.Serialize(subRequest);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        var response = _httpClient.PostAsync($"{_serverUrl}/subscribe", content).Result;
        
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Subscribed to topic: {topic}");
        }
        else
        {
            var jsonMessage = response.Content.ReadAsStringAsync().Result;
            var message = JsonSerializer.Deserialize<MessageResponse>(jsonMessage);
            Console.WriteLine($"Failed to subscribe to topic: {topic}. {message.ErrorMessage}");
        }
    }

    public void Unsubscribe(string topic)
    {
        var unsubRequest = new UnsubscribeRequest
        {
            Topic = topic,
            Token = _token
        };

        var jsonRequest = JsonSerializer.Serialize(unsubRequest);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        var response = _httpClient.PostAsync($"{_serverUrl}/unsubscribe", content).Result;

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Unsubscribed from topic: {topic}");
        }
        else
        {
            Console.WriteLine($"Failed to unsubscribe from topic: {topic}");
        }
    }

    public void SendMessage(string topic, object payload)
    {
        var topicMessage = new TopicMessage
        {
            Topic = topic,
            Payload = payload,
            Token = _token
        };

        var jsonRequest = JsonSerializer.Serialize(topicMessage);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        var response = _httpClient.PostAsync($"{_serverUrl}/send", content).Result;
        
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Sent message to topic {topic}");
        }
        else
        {
            Console.WriteLine($"Failed to send message to topic {topic}");
        }
    }

    public void ReceiveMessages()
    {
        var subRequest = new MessagesRequest
        {
            Token = _token
        };

        var jsonRequest = JsonSerializer.Serialize(subRequest);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        var response = _httpClient.PostAsync($"{_serverUrl}/new_messages", content).Result;

        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = response.Content.ReadAsStringAsync().Result;
            var messages = JsonSerializer.Deserialize<List<TopicMessage>>(jsonResponse);
            foreach (var message in messages)
            {
                Console.WriteLine($"Received message on topic '{message.Topic}': {message.Payload}");
            }
        }
    }

    public void ShowSubscriptions()
    {
        var response = _httpClient.GetAsync($"{_serverUrl}/subscriptions/{_token}").Result;

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Client's topics:");
            var jsonResponse = response.Content.ReadAsStringAsync().Result;
            var topics = JsonSerializer.Deserialize<List<string>>(jsonResponse);
            foreach (var t in topics)
            {
                Console.WriteLine(t);
            }
        }
    }
}