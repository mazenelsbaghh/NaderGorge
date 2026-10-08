using NaderGorge.Application.Features.MimStudio;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public static class MimVideoPrompt
{
    public const string NoWriting = "ABSOLUTELY NO VISIBLE WRITING: no subtitles, captions, titles, names, numbers, dates, labels, signs, logos, watermarks or readable text on books, scrolls, maps, clothing or scenery. Use blank surfaces, non-letter pictograms and visual action. Communicate facts only through spoken dialogue and images.";
    public const string Direction = """
        Create ONE continuous 30-second cinematic 3D cartoon sequence, 16:9, with ten motivated edited shots, NOT a single static talking shot.
        REFERENCE IMAGE 1: Meem's character sheet, identity only. He is the small cream-furred mascot with long teal-tipped ears, large navy eyes, navy adventure suit with teal trim, boots and teal backpack. He is never a monkey or a human.
        REFERENCE IMAGE 2: Papa Nader's approved cartoon character sheet, identity only. Preserve his caricature face, broad high forehead, receding short dark hair, brown eyes, sparse moustache and chin/jaw beard, stout adult build, open white short-sleeve overshirt, white T-shirt, black trousers and black sneakers.
        Papa Nader is Meem's loving fictional father; the fictional character has Alzheimer's, unrelated to any claim about the real teacher. Keep his dignity, agency and teaching knowledge; memory hesitation is brief and Meem responds warmly. Physical comedy comes from objects and situations, never from his condition.
        Both characters remain exactly consistent in model, relative scale, clothing and voice. Use original non-celebrity Egyptian Arabic voices: youthful bright male Meem and warm mature male Papa Nader. Spoken names below indicate the speaker and MUST NOT be read aloud. Preserve exact Arabic dialogue, synchronized lip movement and clear turn taking; no overlapping speech or added narrator.
        Use classic Tom and Jerry-inspired visual slapstick timing, anticipation, elastic motion, fast reactions and musical punctuation, rendered as polished cinematic 3D matching the supplied sheets. No Tom or Jerry characters. Combine wide geography, close reactions, inserts, tracking shots, whip pans, soft bounced lighting, depth and smooth cinematic motion. Keep spatial continuity and readable action.
        Live inside this episode's single ongoing situation. Papa and Meem act, struggle and solve its problem together; educational dialogue grows from what happens. Keep all educational facts grounded in the lesson. Distinguish fictional visual metaphors from historical events; different historical stages are successive passages of time, never simultaneous events.
        No split screen or visible character sheets. Reference sheets do not prescribe the first frame. End at exactly 30 seconds. Leave the first and final half-second for visual action without spoken words; the editor applies fades later. Do not generate fades inside this clip.
        """;

    public static string Build(MimStudioDocument doc, int index)
    {
        var scene = doc.Scenes[index];
        var existing = System.Text.RegularExpressions.Regex.Split(scene.Prompt, @"\nSCENE \d+:|\n\nABSOLUTELY NO VISIBLE WRITING:")[0];
        var direction = scene.Prompt.Contains("\nSCENE ", StringComparison.Ordinal) ? existing : Direction + "\nScene-specific factual direction: " + scene.Prompt;
        return string.Join("\n\n", new[] { direction, NoWriting,
            $"Episode situation: {doc.EpisodeContext ?? doc.Premise}\nCreative direction: {doc.Style}\nContinuity: {doc.Continuity}",
            $"SCENE {index + 1}: {scene.Title}\nScene {index + 1} of {doc.TargetSceneCount}. Educational point: {scene.EducationalPoint}",
            Opening(doc, index), string.Join("\n", scene.Shots.Select(Shot)),
            NoWriting + "\nEnd at exactly 30 seconds. Reserve the first and final half-second for silent visual action. Do not generate fades inside this clip; the editor applies them later. Keep the total spoken dialogue within the timings; lower the score under voices. No overlapping dialogue or added narrator." });
    }

    private static string Opening(MimStudioDocument doc, int index) => index == 0
        ? "Open directly in this episode's situation. Establish the goal and immediate comic problem."
        : "Begin precisely where the previous scene ended; no reset or new introduction. Preserve positions, movement, props, emotional state, lighting and the unfinished goal. Previous scene's final two shots:\n" +
          string.Join("\n", doc.Scenes[index - 1].Shots.TakeLast(2).Select(Shot));

    private static string Shot(MimShot shot) => $"{shot.Start:00}-{shot.End:00}: {shot.Title}\nAction: {shot.Action}\nCamera: {shot.Camera}\nEXACT Egyptian Arabic dialogue (speaker labels are not spoken): {shot.Dialogue}\nSound: {shot.Sound}";
}
