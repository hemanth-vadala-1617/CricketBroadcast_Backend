using System.Text.Json;
namespace CricketStream.Contracts.Graphics;
public class SetGraphicRequest {
    public bool IsVisible { get; set; }
    public JsonElement? Payload { get; set; }   // e.g. { "text": "Happy Independence Day", "imageUrl": "/uploads/x.png" }
}
