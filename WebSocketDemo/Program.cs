// See https://aka.ms/new-console-template for more information

using System.Text;
using System.Text.Json;
using WebSocketDemo;

// 公共 WebSocket 服务器示例：https://socketsbay.com/test-websockets
const string webSocketGatewayUrl = "wss://socketsbay.com/wss/v2/1/demo/";

// // 直接使用 WebSocket 客户端
// DefaultWebSocketClient webSocketClient = new();
// webSocketClient.TextMessage += x =>
// {
//     Console.WriteLine(x);
//     return Task.CompletedTask;
// };
// await webSocketClient.ConnectAsync(webSocketGatewayUrl);
// byte[] bytes = Encoding.Default.GetBytes("This message is sent via a custom C# websocket implementation.");
// await webSocketClient.SendAsync(bytes, 0, bytes.Length, true);

// 使用封装了 WebSocket 交互逻辑的客户端
// BusinessSocketApiClient businessSocketApiClient = new(DefaultWebSocketProvider.Instance, webSocketGatewayUrl);
// businessSocketApiClient.ReceivedGatewayEvent += x =>
// {
//     Console.WriteLine($"Received: {JsonSerializer.Serialize(x)}");
//     return Task.CompletedTask;
// };
// businessSocketApiClient.SentGatewayMessage += x =>
// {
//     Console.WriteLine($"Sent: {x}");
//     return Task.CompletedTask;
// };
// businessSocketApiClient.Disconnected += x =>
// {
//     Console.WriteLine($"Disconnected: {x.Message}");
//     return Task.CompletedTask;
// };
// await businessSocketApiClient.ConnectAsync();
// await businessSocketApiClient.SendGatewayAsync("This message is sent via a custom C# websocket implementation.");

// 使用封装了业务逻辑的客户端
BusinessSocketClient businessSocketClient = new(webSocketGatewayUrl, JsonSerializerOptions.Default);
businessSocketClient.Connected += () => Task.CompletedTask;
businessSocketClient.Disconnected += x => Task.CompletedTask;
businessSocketClient.Ready += async () =>
{
    // 发送消息也应该封装方法在 BusinessSocketClient 里，这里只是示例调用内部的 WebSocketApiClient 未封装逻辑的方法
    await businessSocketClient.ApiClient.SendGatewayAsync("This message is sent via a custom C# websocket implementation.");
};
businessSocketClient.Log += x =>
{
    Console.WriteLine(x);
    return Task.CompletedTask;
};
// 这里再处理事件就应该都是封装好的业务逻辑了，逻辑封装在 ProcessMessageAsync 里，然后在这里订阅事件
// businessSocketClient.ReceiveOnePackage += x =>
// {
//     Console.WriteLine($"Received: {JsonSerializer.Serialize(x)}");
//     return Task.CompletedTask;
// };
// businessSocketClient.ReceiveAnotherPackage += x =>
// {
//     Console.WriteLine($"Received: {JsonSerializer.Serialize(x)}");
//     return Task.CompletedTask;
// };
await businessSocketClient.StartAsync();

// 防止程序退出
await Task.Delay(Timeout.Infinite);
