public class Program
{
    private static Client _client;
    private static bool _isLoggedIn = false;
    private static string _currentUsername = "";
    private static string _serverUrl;
    private static bool _exitRequested = false; // Флаг для корректного завершения

    public static void Main(string[] args)
    {
        Console.WriteLine("--- Messenger Client ---");

        GetServerSettings();

        _client = new Client(_serverUrl);

        RunClientUI();
    }

    private static void GetServerSettings()
    {
        Console.Write("Enter server URL (default: http://localhost:8080): ");
        _serverUrl = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(_serverUrl)) _serverUrl = "http://localhost:8080";
    }

    private static void RunClientUI()
    {
        // Попытка авторизации
        if (!Login())
        {
            Console.WriteLine("Login failed. Press Enter to exit.");
            Console.ReadLine();
            return;
        }

        // Основной цикл UI
        while (_isLoggedIn && !_exitRequested)
        {
            ShowMenu();
            string choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    SubscribeToTopic();
                    break;
                case "2":
                    UnsubscribeFromTopic();
                    break;
                case "3":
                    SendMessage();
                    break;
                case "4":
                    ReceiveMessages();
                    break;
                case "5":
                    ShowTopics();
                    break;
                case "6":
                    Logout();
                    break;
                case "7":
                    _exitRequested = true;
                    Console.WriteLine("Exiting application...");
                    break;
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    break;
            }
        }

        Console.WriteLine();
        if (_isLoggedIn)
        {
            Console.WriteLine("Disconnecting...");
        }
    }

    private static bool Login()
    {
        Console.Write("Enter username: ");
        string username = Console.ReadLine();
        Console.Write("Enter password: ");
        string password = Console.ReadLine();

        if (_client.Authorize(username, password))
        {
            _isLoggedIn = true;
            _currentUsername = username;
            Console.WriteLine($"Welcome, {_currentUsername}!");
            return true;
        }
        else
        {
            return false;
        }
    }

    private static void Logout()
    {
        _isLoggedIn = false;
        Console.WriteLine("You have been logged out.");
        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
        RunClientUI();
    }

    private static void ShowMenu()
    {
        Console.WriteLine("\n--- Main Menu ---");
        Console.WriteLine($"Logged in as: {_currentUsername} | Server: {_serverUrl}");
        Console.WriteLine("1. Subscribe to topic");
        Console.WriteLine("2. Unsubscribe from topic");
        Console.WriteLine("3. Send message");
        Console.WriteLine("4. Receive message");
        Console.WriteLine("5. Show subscriptions");
        Console.WriteLine("6. Logout");
        Console.WriteLine("7. Exit");
        Console.Write("Enter your choice: ");
    }

    private static void SubscribeToTopic()
    {
        Console.Write("Enter topic to subscribe to: ");
        string topic = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(topic))
        {
            _client.Subscribe(topic);
        }
        else
        {
            Console.WriteLine("Topic cannot be empty.");
        }
    }

    private static void UnsubscribeFromTopic()
    {
        Console.Write("Enter topic to unsubscribe from: ");
        string topic = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(topic))
        {
            _client.Unsubscribe(topic);
        }
        else
        {
            Console.WriteLine("Topic cannot be empty.");
        }
    }

    private static void SendMessage()
    {
        Console.Write("Enter topic to send message to: ");
        string topic = Console.ReadLine()?.Trim();
        Console.Write("Enter message payload (simple text for now): ");
        string payload = Console.ReadLine();

        if (!string.IsNullOrEmpty(topic) && payload != null)
        {
            _client.SendMessage(topic, payload);
        }
        else
        {
            Console.WriteLine("Topic and payload cannot be empty.");
        }
    }

    private static void ReceiveMessages()
    {
        _client.ReceiveMessages();
    }

    private static void ShowTopics()
    {
        _client.ShowSubscriptions();
    }
}