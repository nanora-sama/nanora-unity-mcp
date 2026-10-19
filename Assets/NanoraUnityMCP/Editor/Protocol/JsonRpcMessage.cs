using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NanoraUnityMCP.Protocol
{
    /// <summary>
    /// JSON-RPC 2.0 request message.
    /// </summary>
    public class JsonRpcRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public JToken Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public JToken Params { get; set; }

        public bool IsNotification => Id == null;
    }

    /// <summary>
    /// JSON-RPC 2.0 response message.
    /// </summary>
    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id", NullValueHandling = NullValueHandling.Include)]
        public JToken Id { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public JsonRpcError Error { get; set; }

        public static JsonRpcResponse Success(JToken id, object result)
        {
            return new JsonRpcResponse { Id = id, Result = result };
        }

        public static JsonRpcResponse CreateError(JToken id, int code, string message, object data = null)
        {
            return new JsonRpcResponse
            {
                Id = id,
                Error = new JsonRpcError { Code = code, Message = message, Data = data }
            };
        }
    }

    /// <summary>
    /// JSON-RPC 2.0 error object.
    /// </summary>
    public class JsonRpcError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; set; }
    }

    /// <summary>
    /// Standard JSON-RPC error codes.
    /// </summary>
    public static class JsonRpcErrorCodes
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;
    }
}
