using System;

// Атрибут Serializable не обязателен для Newtonsoft.Json, но может быть полезен
// для других сценариев сериализации.
[Serializable]
public abstract class Message
{
    public abstract string Type { get; }
}

[Serializable]
public class TopicMessage : Message
{
    public override string Type => "TopicMessage";
    public string Topic { get; set; }
    public object Payload { get; set; } // Может быть любым сериализуемым объектом
}

[Serializable]
public class SubscribeRequest : Message
{
    public override string Type => "SubscribeRequest";
    public string Topic { get; set; }
}

[Serializable]
public class UnsubscribeRequest : Message
{
    public override string Type => "UnsubscribeRequest";
    public string Topic { get; set; }
}

[Serializable]
public class AuthorizationRequest : Message
{
    public override string Type => "AuthorizationRequest";
    public string Username { get; set; }
    public string Password { get; set; }
    
    public override string ToString()
    {
        return $"Userame: {Username}, Password: {Password}";
    }
}

[Serializable]
public class AuthorizationResponse : Message
{
    public override string Type => "AuthorizationResponse";
    public bool IsAuthorized { get; set; }
    public string Token { get; set; } // Токен для дальнейшей авторизации
}

[Serializable]
public class MessageResponse : Message
{
    public override string Type => "MessageResponse";
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
}