using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class GestureRecorder : MonoBehaviour
{
    public float recordDuration = 3f;
    public TMP_Text countdownText;
    public bool IsRecording { get; private set; }

    InputDevice rightController;
    List<Vector3> recordedPositions;
    bool waveMode = true;
    bool aborChoice;

    void Start()
    {
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (countdownText != null)
            countdownText.text = "Draw with right trigger";
    }

    void Haptic(float amplitude, float duration)
    {
        rightController.SendHapticImpulse(0, amplitude, duration);
    }

    public void StartRecording(GameObject stroke)
    {
        StartCoroutine(ConfirmThenRecord(stroke));
    }

    IEnumerator ConfirmThenRecord(GameObject stroke)
    {
        IsRecording = true;

        if (countdownText != null)
            countdownText.text = "Keep this stroke?\n+ Trigger   x Grip";

        while (true)
        {
            if (!rightController.isValid)
                rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            rightController.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
            rightController.TryGetFeatureValue(CommonUsages.grip, out float grip);

            if (trigger > 0.8f)
            {
                Haptic(0.4f, 0.3f);
                break;
            }
            if (grip > 0.8f)
            {
                Haptic(0.8f, 0.08f);
                yield return new WaitForSeconds(0.1f);
                Haptic(0.8f, 0.08f);
                Destroy(stroke);
                IsRecording = false;
                if (countdownText != null)
                    countdownText.text = "Draw with right trigger";
                yield break;
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        yield return StartCoroutine(SelectMode());

        var renderers = stroke.GetComponentsInChildren<MeshRenderer>();
        Material[] originalMats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalMats[i] = renderers[i].material;

        bool shapeAccepted = false;
        while (!shapeAccepted)
        {
            yield return StartCoroutine(RecordGesture("Shape"));

            var playback = stroke.AddComponent<GesturePlayback>();
            playback.Init(recordedPositions, waveMode);

            if (countdownText != null)
                countdownText.text = "Keep this shape?\n+ Trigger   x Grip";

            yield return new WaitForSeconds(0.3f);
            yield return StartCoroutine(WaitForTriggerRelease());

            while (true)
            {
                rightController.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
                rightController.TryGetFeatureValue(CommonUsages.grip, out float grip);

                if (trigger > 0.8f)
                {
                    Haptic(0.4f, 0.3f);
                    shapeAccepted = true;
                    break;
                }
                if (grip > 0.8f)
                {
                    Haptic(0.8f, 0.08f);
                    yield return new WaitForSeconds(0.1f);
                    Haptic(0.8f, 0.08f);
                    Destroy(playback);
                    for (int i = 0; i < renderers.Length; i++)
                        renderers[i].material = originalMats[i];
                    yield return StartCoroutine(SelectMode());
                    break;
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
        }

        var gesturePlayback = stroke.GetComponent<GesturePlayback>();

        if (countdownText != null)
            countdownText.text = "Add rhythm layer?\nA: Yes   B: Skip";

        yield return StartCoroutine(WaitForAorB());
        if (aborChoice)
        {
            bool speedAccepted = false;
            while (!speedAccepted)
            {
                yield return StartCoroutine(RecordGesture("Rhythm"));

                float speed = ExtractSpeed(recordedPositions);
                gesturePlayback.SetSpeed(speed);

                if (countdownText != null)
                    countdownText.text = "Keep this rhythm?\n+ Trigger   x Grip";

                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(WaitForTriggerRelease());

                while (true)
                {
                    rightController.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
                    rightController.TryGetFeatureValue(CommonUsages.grip, out float grip);

                    if (trigger > 0.8f)
                    {
                        Haptic(0.4f, 0.3f);
                        speedAccepted = true;
                        break;
                    }
                    if (grip > 0.8f)
                    {
                        Haptic(0.8f, 0.08f);
                        yield return new WaitForSeconds(0.1f);
                        Haptic(0.8f, 0.08f);
                        gesturePlayback.SetSpeed(0.2f);
                        break;
                    }
                    yield return null;
                }
                yield return new WaitForSeconds(0.3f);
            }
        }

        if (countdownText != null)
            countdownText.text = "Looking good!\n+ Trigger to finish   x Grip to restart";

        yield return StartCoroutine(WaitForTriggerRelease());

        while (true)
        {
            rightController.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
            rightController.TryGetFeatureValue(CommonUsages.grip, out float grip);

            if (trigger > 0.8f)
            {
                Haptic(0.4f, 0.5f);
                break;
            }
            if (grip > 0.8f)
            {
                Haptic(0.8f, 0.08f);
                yield return new WaitForSeconds(0.1f);
                Haptic(0.8f, 0.08f);
                Destroy(gesturePlayback);
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].material = originalMats[i];
                yield return StartCoroutine(ConfirmThenRecord(stroke));
                yield break;
            }
            yield return null;
        }

        yield return StartCoroutine(WaitForTriggerRelease());

        IsRecording = false;
        if (countdownText != null)
            countdownText.text = "Draw with right trigger";
    }

    IEnumerator WaitForTriggerRelease()
    {
        while (true)
        {
            rightController.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
            if (trigger < 0.1f) break;
            yield return null;
        }
    }

    IEnumerator SelectMode()
    {
        if (countdownText != null)
            countdownText.text = "Animation style:\nA: Wave — travels end to end\nB: Pulse — moves all at once";

        while (true)
        {
            rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed);
            rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bPressed);

            if (aPressed) { waveMode = true; break; }
            if (bPressed) { waveMode = false; break; }
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator WaitForAorB()
    {
        while (true)
        {
            rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed);
            rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bPressed);

            if (aPressed) { aborChoice = true; break; }
            if (bPressed) { aborChoice = false; break; }
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator RecordGesture(string prompt)
    {
        string description = prompt == "Rhythm" ? "Move fast or slow — sets the animation pace" :
                            "Move your hand freely — this shapes the animation";

        if (countdownText != null)
            countdownText.text = prompt + " Gesture\n" + description + "\nGet ready...";
        yield return new WaitForSeconds(1f);

        Haptic(0.8f, 0.1f);

        recordedPositions = new List<Vector3>();
        float elapsed = 0f;

        var rightHand = UnityEngine.InputSystem.XR.XRController.rightHand;

        GameObject trailObj = new GameObject("GestureTrail");
        TrailRenderer trail = trailObj.AddComponent<TrailRenderer>();
        trail.time = recordDuration;
        trail.startWidth = 0.008f;
        trail.endWidth = 0.002f;
        trail.minVertexDistance = 0.001f;

        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        trailMat.color = prompt.Contains("Rhythm") ? Color.yellow : Color.cyan;
        trail.material = trailMat;

        while (elapsed < recordDuration)
        {
            if (rightHand != null)
            {
                Vector3 currentPos = rightHand.devicePosition.ReadValue();
                recordedPositions.Add(currentPos);
                trailObj.transform.position = currentPos;
            }

            if (countdownText != null)
            {
                float remaining = recordDuration - elapsed;
                countdownText.text = "Recording " + prompt.ToLower() + "...  " + Mathf.CeilToInt(remaining) + "s\n" + description;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Haptic(0.6f, 0.08f);
        yield return new WaitForSeconds(0.15f);
        Haptic(0.6f, 0.08f);

        Destroy(trailObj);
    }

    float ExtractSpeed(List<Vector3> positions)
    {
        float totalDistance = 0f;
        for (int i = 1; i < positions.Count; i++)
            totalDistance += Vector3.Distance(positions[i], positions[i - 1]);
        float speed = totalDistance / recordDuration;
        return Mathf.Clamp(speed * 0.5f, 0.05f, 0.8f);
    }
}