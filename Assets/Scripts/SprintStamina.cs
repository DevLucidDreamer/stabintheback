using UnityEngine;

[System.Serializable]
public sealed class SprintStamina
{
    public float sprintSeconds = 4.5f, recoverySeconds = 6f, recoveryDelay = 1.2f;
    public float Value { get; private set; } = 1f;
    public bool Exhausted { get; private set; }
    private float restTime;
    public bool Tick(float dt, bool held, bool moving)
    {
        if (!float.IsFinite(dt) || dt <= 0) return false;
        if (!held && Value >= .3f) Exhausted = false;
        bool running = held && moving && !Exhausted && Value > 0;
        if (running)
        {
            Value = Mathf.Max(0, Value - dt / Mathf.Max(.1f, sprintSeconds)); restTime = 0;
            if (Value <= 0) Exhausted = true;
        }
        else
        {
            float previous = restTime; restTime += dt;
            float recoverDt = Mathf.Max(0, restTime - recoveryDelay) - Mathf.Max(0, previous - recoveryDelay);
            Value = Mathf.Min(1, Value + recoverDt / Mathf.Max(.1f, recoverySeconds));
        }
        return running;
    }
    public void Reset() { Value = 1; Exhausted = false; restTime = 0; }
}
