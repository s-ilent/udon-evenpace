
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace SilentTools
{
[RequireComponent(typeof(AudioSource))]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class EvenPace : UdonSharpBehaviour
{
    [Header("Audio Clips")]
    public AudioClip[] step;
    public AudioClip[] jumpLanding;
    public AudioClip[] longFallLanding;
    [Tooltip("Please create an AudioSource as a child of this object and assign it to this field with the desired falling sound.")]
    public AudioSource fallingLoopSound;

    [Header("Stepping")]
    [SerializeField] 
    [Tooltip("The baseline volume of each footstep.")]
    private float stepBaseVolume = 0.18f;
    [SerializeField] 
    [Tooltip("How much extra volume to add to each footstep based on the player's velocity.")]
    private float stepVolumeMultiplier = 0.15f; 
    [SerializeField]
    [Tooltip("The minimum velocity for step distance calculations.")]
    private float stepLowVelocity = 2.0f;
    [SerializeField]
    [Tooltip("The maximum velocity for step distance calculations.")]
    private float stepHighVelocity = 3.0f;
    [SerializeField]
    [Tooltip("The distance it takes before a new footstep is played, at the minimum velocity.")]
    private float stepLowDistance = 1.0f;
    [SerializeField]
    [Tooltip("The distance it takes before a new footstep is played, at the maximum velocity.")]
    private float stepHighDistance = 1.2f;
    [SerializeField]
    [Tooltip("The rate at which step distance increases with velocity.")]
    private float stepDistanceIncrement = 1.2f;

    [Header("Landing")]
    [SerializeField] 
    [Tooltip("The minimum time that must elapse before the landing sound will play again.")]
    private float landingMinTimeBetween = 0.2f;
    [SerializeField] 
    [Tooltip("The minimum Y velocity the player must reach before the landing sound will play.")]
    private float landingVelocityCutoff = 0.2f;
    [SerializeField] 
    [Tooltip("The minimum Y velocity the player must reach before the long fall landing sound will play.")]
    private float landingFallVelocityCutoff = 7.8f;
    [SerializeField] 
    private float landingBaseVolume = 0.01f;
    [SerializeField] 
    [Tooltip("How much extra volume to add to the landing sound based on the player's velocity.")]
    private float landingVolumeMultiplier = 0.10f;

    [Header("Other")]
    [SerializeField] 
    private float volumeAdjust = 1.0f;

    // Internal vars

    [Header("Debug")]
    [SerializeField] 
    private bool _isEditor = false;

    [SerializeField] 
    private Vector3 _lastFootLoc;
    [SerializeField] 
    private float _lastFootTime;
    
    
    [SerializeField] 
    private float _airTimer = 0;
    [SerializeField] 
    private Vector3 _preLandingVelocity;
    [SerializeField] 
    private bool _wasAirborneLastFrame = false;

    private VRCPlayerApi _playerLocal;
    private AudioSource _sndSource;

    void Start()
    {
        _playerLocal = Networking.LocalPlayer;
        if (_playerLocal == null)
        {
            _isEditor = true;
        }
        _sndSource = (AudioSource)gameObject.GetComponent(typeof(AudioSource));
    }

    bool IsOnGround(GameObject obj, float distanceToGround = 0.1f)
    {
        // Technically, we can query the player object to see whether it is grounded,  
        // but there are cases where that doesn't work...
        Ray ray = new Ray(obj.transform.position + distanceToGround * Vector3.up, -Vector3.up);
        bool isOnGround = Physics.Raycast(ray, distanceToGround);

        return isOnGround;
    }

    private Vector3 GetPlayerFootPosition()
    {
        // The footstep handler is moved on FixedUpdate to match the player position,
        // which should be at the base of the player object - so, roughly at their feet. 
        return transform.position;
    }

    private void DoLandingSound(float playVol, Vector3 footLoc)
    {
        _sndSource.PlayOneShot(jumpLanding[Random.Range(0, jumpLanding.Length)], playVol);
    }

    private void DoLongFallLandingSound(float playVol, Vector3 footLoc)
    {
        _sndSource.PlayOneShot(longFallLanding[Random.Range(0, longFallLanding.Length)], playVol);
    }

    private void DoStepSound(float playVol, Vector3 footLoc)
    {
        _sndSource.PlayOneShot(step[Random.Range(0, step.Length)], playVol);
    }

    public void LeaveGround()
    {
        _lastFootLoc = GetPlayerFootPosition();
        _lastFootTime = Time.time;
    }

    public void LandOnGround(Vector3 velocity)
    {
        if (_isEditor)
            return;

        // Has there been enough time since our last landing?
        if ((Time.time - _lastFootTime) > landingMinTimeBetween)
        {
            Vector3 footLoc;

            footLoc = GetPlayerFootPosition();

            Debug.Log($"Our Y velocity is {velocity.y} and the cutoff is {-landingVelocityCutoff}; condition {(velocity.y < -landingVelocityCutoff)}");

            if (volumeAdjust > 0)
            {
                float playVol = landingBaseVolume - landingVolumeMultiplier * (landingVelocityCutoff + velocity.y);
                if (velocity.y < -landingVelocityCutoff)
                {
                    if (velocity.y < -landingFallVelocityCutoff)
                    {
                        DoLongFallLandingSound(playVol * volumeAdjust, footLoc);
                    }
                    else
                    {
                        DoLandingSound(playVol * volumeAdjust, footLoc);
                    }
                }
                else
                {
                    DoStepSound(playVol * volumeAdjust, footLoc);
                }
            }
        }
    }

    public void LandOnGround()
    {
        if (_isEditor)
            return;

        Vector3 velocity = _playerLocal.GetVelocity();
        LandOnGround(velocity);
    }
    

    private void Update()
    {
        if (_isEditor)
            return;
        
        // Move to player's base position
        transform.position = _playerLocal.GetPosition();

        if (!_playerLocal.IsPlayerGrounded())
        {
            _airTimer += Time.fixedDeltaTime;

            if (!_wasAirborneLastFrame) 
            {
                LeaveGround();
            }
            _preLandingVelocity = _playerLocal.GetVelocity(); 
            _wasAirborneLastFrame = true;

            // Falling sound
            var volumeTarget = _preLandingVelocity.magnitude / 100f;
            if (_preLandingVelocity.magnitude > 8f)
            {
                fallingLoopSound.volume = Mathf.Lerp(fallingLoopSound.volume, volumeTarget, 0.1f); // Set based on velocity
            }
            else
            {
                fallingLoopSound.volume = Mathf.Lerp(fallingLoopSound.volume, 0f, 0.05f); // Fade out if slowing down

            }
        }
        else // Player is grounded
        {
            if (_airTimer > 0f)  // Player was airborne in the previous frame
            {
                _airTimer = 0f;
                fallingLoopSound.volume = 0f; // Stop any falling sound
                LandOnGround(_preLandingVelocity); // Call LandOnGround with the pre-landing velocity
            }
            _wasAirborneLastFrame = false;
        }
                
        // TODO: If the ground is moving underneath us... do we need to handle that? 

        Vector3 velocity = _playerLocal.GetVelocity();
        float velocityMag = velocity.magnitude;

        float footstepDist;

        if (velocityMag < stepLowVelocity)
            footstepDist = stepLowDistance;
        else if (velocityMag > stepHighVelocity)
            footstepDist = stepHighDistance;
        else
            footstepDist = stepLowDistance + stepDistanceIncrement * ((velocityMag - stepLowVelocity) / (stepHighVelocity - stepLowVelocity));

        Vector3 footLoc = GetPlayerFootPosition();

        // Have we moved enough to step?
        float curDist2 = Vector3.Distance(_lastFootLoc, footLoc);

        if ((velocityMag > (0.25f)) && ((_lastFootTime < 0) || (curDist2 > Mathf.Pow(footstepDist, 2))))
        {
            bool onGround = IsOnGround(this.gameObject);

            // Update our last footfall location/time when
            // player is not jumping and player is grounded or not standing
            if (_playerLocal.IsPlayerGrounded() && onGround) 
            {
                _lastFootLoc = footLoc;
                _lastFootTime = Time.time;
            }

            if (velocityMag > 1.5)
            {
                bool doStep = true;
/*
                // Player is terrestrial...
                if (_playerLocal.IsPlayerGrounded()) 
                {
                    // ...but not actually on the ground
                    if (!IsOnGround(this.gameObject))
                    {
                        doStep = false;
                        Debug.Log("Stride cancelled due to non-contact with ground");
                    }
                }
*/
                if (!_playerLocal.IsPlayerGrounded()) doStep = false;

                if (doStep)
                {
                    if (volumeAdjust > 0)
                    {
                        float playVol = stepBaseVolume + (stepVolumeMultiplier * velocityMag);

                        DoStepSound(playVol * volumeAdjust, footLoc);
                    }
                }
            }
        }

        // See if we're coming to a stop
        if (((Time.time - _lastFootTime) > 1.00f) && (velocityMag > 0.1) &&
            ((curDist2 > Mathf.Pow(footstepDist / 2.0f, 2)) || (velocityMag < 1.5)))
        {
            // Todo: Something here
        }

        // Have we stopped moving?      
        if (velocityMag < 1.0)
            _lastFootTime = -1.0f;
    }
}
}
