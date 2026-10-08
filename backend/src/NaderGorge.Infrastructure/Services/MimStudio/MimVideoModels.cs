using System.Text.Json;
using System.Text.Json.Nodes;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public sealed record MimVideoModel(string Id, string Name);
public sealed record MimVideoQuoteRequest(string Model = "seedance_2_5");
public sealed record MimVideoQuoteTarget(Guid Lesson, int Scene, string Model);

public static class MimVideoModels
{
    public static readonly MimVideoModel[] Choices = [new("wan3_0_prime", "Wan 3.0 Prime"), new("seedance_2_5", "Seedance 2.5")];

    public static MimVideoModel Require(string id) => Choices.SingleOrDefault(x => x.Id == id)
        ?? throw new ArgumentException("اختار موديل فيديو من القائمة المتاحة.");

    public static void ValidateCapabilities(JsonElement model, string id)
    {
        var valid = model.TryGetProperty("id", out var modelId) && modelId.ValueKind == JsonValueKind.String && modelId.GetString() == id &&
            model.TryGetProperty("aspect_ratios", out var ratios) && ratios.ValueKind == JsonValueKind.Array && ratios.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == "16:9") &&
            model.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array &&
            parameters.EnumerateArray().Any(x => x.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() == "duration" &&
                x.TryGetProperty("max", out var max) && max.ValueKind == JsonValueKind.Number && max.TryGetDouble(out var seconds) && seconds >= 30) &&
            model.TryGetProperty("medias", out var medias) && medias.ValueKind == JsonValueKind.Array &&
            medias.EnumerateArray().Any(x => x.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array &&
                roles.EnumerateArray().Any(role => role.ValueKind == JsonValueKind.String && role.GetString() == "image_references"));
        if (!valid) throw new HiggsfieldMcpException("الموديل المختار لا يؤكد دعم مشهد ٣٠ ثانية بمراجع الشخصيات حاليًا. اختار موديلًا آخر أو حدّث التكلفة لاحقًا.");
    }

    public static JsonObject Parameters(string model, string prompt, JsonArray medias)
    {
        Require(model);
        var parameters = new JsonObject { ["model"] = model, ["prompt"] = prompt, ["count"] = 1,
            ["duration"] = 30, ["aspect_ratio"] = "16:9", ["resolution"] = "720p",
            ["generate_audio"] = true, ["use_unlim"] = false, ["medias"] = medias };
        if (model == "seedance_2_5")
        {
            parameters["mode"] = "omni_reference";
            parameters["bitrate_mode"] = "standard";
            parameters["draft"] = false;
        }
        return parameters;
    }
}
