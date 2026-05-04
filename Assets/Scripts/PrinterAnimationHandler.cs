using UnityEngine;

public class PrinterAnimationHandler : MonoBehaviour
{
    private Animator _animator;
    private string _cachedDeviceId;
    private bool _isCurrentlyPrinting = false;

    private const string IS_PRINTING_PARAM = "IsPrinting";
    private const string PRINT_STATE_NAME = "PlayAnimation"; 

    void Awake()
    {
        _animator = GetComponent<Animator>();
        
        if (_animator == null)
            Debug.LogError($"[AnimHandler] No Animator found on {gameObject.name}!");

        PrinterObject printerData = GetComponentInParent<PrinterObject>();
        if (printerData != null)
        {
            _cachedDeviceId = printerData.deviceId;
        }
    }

    public string GetDeviceId() => _cachedDeviceId;

    public void SetRunningState(bool shouldBeRunning)
    {
        if (_animator == null) return;

        _animator.SetBool(IS_PRINTING_PARAM, shouldBeRunning);

        if (shouldBeRunning && !_isCurrentlyPrinting)
        {
            _isCurrentlyPrinting = true;
            
            // if you re-export the animations from blender and they aren't exactly 250 frames anymore, update this value!
            // we start at a random frame so the entire print farm doesn't bob up and down in perfect sync like robots
            float totalFrames = 250f; 
            float randomFrame = Random.Range(10f, totalFrames);
            float normalizedTime = randomFrame / totalFrames;

            _animator.Play(PRINT_STATE_NAME, 0, normalizedTime);
            
            Debug.Log($"[Anim] {gameObject.name} started at frame: {randomFrame}");
        }
        else if (!shouldBeRunning && _isCurrentlyPrinting)
        {
            _isCurrentlyPrinting = false;
        }
    }
}