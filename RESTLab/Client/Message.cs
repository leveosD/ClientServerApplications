[Serializable]
public abstract class Message
{
    public abstract string Token { get; set; }
    public abstract string Type { get; }
}

[Serializable]
public class TopicMessage : Message
{
    public override string Token { get; set; }
    public override string Type => "TopicMessage";
    public string Topic { get; set; }
    public object Payload { get; set; } // Может быть любым сериализуемым объектом
}

public class MessagesRequest : Message
{
    public override string Token { get; set; }
    public override string Type => "MessagesRequest";
}

[Serializable]
public class SubscribeRequest
{
    public string Token { get; set; }
    public string Type => "SubscribeRequest";
    public string Topic { get; set; }
}

[Serializable]
public class UnsubscribeRequest
{
    public string Type => "UnsubscribeRequest";
    public string Token { get; set; }
    public string Topic { get; set; }
}

[Serializable]
public class AuthorizationRequest
{
    public string Type => "AuthorizationRequest";
    public string Username { get; set; }
    public string Password { get; set; }
}

[Serializable]
public class AuthorizationResponse
{
    public string Type => "AuthorizationResponse";
    public bool IsAuthorized { get; set; }
    public string Token { get; set; } // Токен для дальнейшей авторизации
}

[Serializable]
public class MessageResponse
{
    public string Type => "MessageResponse";
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
}