using UnityEngine;

[DisallowMultipleComponent]
public class DeviceSelector : MonoBehaviour
{
    private const float ClickMovementThreshold = 4f;
    private const float RepeatDelay = 0.5f;

    public static DeviceSelector Instance { get; private set; }

    private DeviceSelectable selected;
    private Camera selectionCamera;
    private bool pressedEmptySpace;
    private Vector2 pressPosition;

    private bool moveHeld;
    private float moveHoldTime;
    private Vector2 lastMoveInput;
    private bool rotateHeld;
    private float rotateHoldTime;
    private float lastRotateInput;

    public DeviceSelectable Selected => selected;

    private void Awake()
    {
        Instance = this;
        selectionCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            DeviceSelectable picked = DevicePicker.Pick(Input.mousePosition, selectionCamera);

            if (picked != null)
            {
                Select(picked);
                pressedEmptySpace = false;
            }
            else
            {
                // Pressing empty space starts a box drag; drop the current device
                // selection as soon as the drag gesture begins.
                Select(null);
                pressedEmptySpace = true;
                pressPosition = Input.mousePosition;
            }
        }

        if (pressedEmptySpace && Input.GetMouseButtonUp(0))
        {
            var moveDelta = new Vector2(Input.mousePosition.x, Input.mousePosition.y) - pressPosition;
            pressedEmptySpace = false;

            if (moveDelta.sqrMagnitude < ClickMovementThreshold * ClickMovementThreshold)
            {
                Select(null);
            }
        }

        if (selected == null)
        {
            return;
        }

        if (selected.CanMove)
        {
            // A/D and W/S describe screen-space directions; the device projects them onto
            // its own sliding axis, so the controls stay relative to the player's view.
            float horizontal = 0f;
            float vertical = 0f;

            if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.D)) horizontal += 1f;
            if (Input.GetKey(KeyCode.S)) vertical -= 1f;
            if (Input.GetKey(KeyCode.W)) vertical += 1f;

            var screenInput = new Vector2(horizontal, vertical);

            if (screenInput.sqrMagnitude > 0.001f)
            {
                bool newDirection = !moveHeld
                    || Vector2.Dot(screenInput.normalized, lastMoveInput.normalized) < 0.995f;

                if (newDirection)
                {
                    // A tap uses one continuous frame's step; holding the key
                    // starts continuous movement only after RepeatDelay.
                    selected.MoveAlongWall(screenInput, Time.deltaTime);
                    moveHoldTime = 0f;
                }
                else
                {
                    moveHoldTime += Time.deltaTime;

                    if (moveHoldTime >= RepeatDelay)
                    {
                        selected.MoveAlongWall(screenInput, Time.deltaTime);
                    }
                }

                lastMoveInput = screenInput;
                moveHeld = true;
            }
            else
            {
                moveHeld = false;
                moveHoldTime = 0f;
            }
        }

        if (selected.CanRotate)
        {
            float rotate = 0f;

            if (Input.GetKey(KeyCode.LeftArrow)) rotate += 1f;
            if (Input.GetKey(KeyCode.RightArrow)) rotate -= 1f;

            if (Mathf.Abs(rotate) > 0.001f)
            {
                bool newDirection = !rotateHeld
                    || Mathf.Sign(rotate) != Mathf.Sign(lastRotateInput);

                if (newDirection)
                {
                    selected.Rotate(rotate * selected.RotateSpeed * Time.deltaTime);
                    rotateHoldTime = 0f;
                }
                else
                {
                    rotateHoldTime += Time.deltaTime;

                    if (rotateHoldTime >= RepeatDelay)
                    {
                        selected.Rotate(rotate * selected.RotateSpeed * Time.deltaTime);
                    }
                }

                lastRotateInput = rotate;
                rotateHeld = true;
            }
            else
            {
                rotateHeld = false;
                rotateHoldTime = 0f;
            }
        }
    }

    private void Select(DeviceSelectable device)
    {
        if (selected == device)
        {
            return;
        }

        if (selected != null)
        {
            selected.SetSelected(false);
        }

        moveHeld = false;
        moveHoldTime = 0f;
        rotateHeld = false;
        rotateHoldTime = 0f;
        selected = device;

        if (selected != null)
        {
            selected.SetSelected(true);
        }
    }
}
