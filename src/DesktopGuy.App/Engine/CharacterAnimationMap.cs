namespace DesktopGuy.App.Engine;

/// <summary>
/// The one place that knows which character.json animation row name goes
/// with which behavior state. Shared by CharacterController (to check
/// whether a character even defines an animation before switching to a
/// state that needs it) and MainWindow (to tell the SpriteAnimator what
/// to play).
/// </summary>
public static class CharacterAnimationMap
{
    public static string GetAnimationName(CharacterState state) => state switch
    {
        CharacterState.Idle => "idle",
        CharacterState.Walking => "walk",
        CharacterState.Sleeping => "sleep",
        CharacterState.Waking => "wake",
        CharacterState.Dragging => "drag",
        CharacterState.Dancing => "dance",
        CharacterState.Watching => "watch",
        CharacterState.AnsweringCall => "answerCall",
        CharacterState.ReadingMessage => "openMail",
        _ => "idle",
    };
}
