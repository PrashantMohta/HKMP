﻿using System.Collections;
using HKMP.Networking.Packet;
using HKMP.Util;
using HutongGames.PlayMaker.Actions;
using ModCommon;
using ModCommon.Util;
using UnityEngine;

namespace HKMP.Animation.Effects {
    public abstract class FireballBase : IAnimationEffect {

        public abstract void Play(GameObject playerObject, Packet packet);
        
        protected void Play(
            GameObject playerObject, 
            Packet packet, 
            string fireballParentName, 
            int castFireballIndex, 
            int castAudioIndex, 
            int dungFlukeIndex, 
            int dungFlukeAudioIndex, 
            float baseFireballSize,
            bool noFireballFlip
        ) {
            // Read the necessary data to create this effect
            var hasFlukenestCharm = packet.ReadBool();
            var hasDefenderCrestCharm = packet.ReadBool();
            var hasShamanStoneCharm = packet.ReadBool();

            // Obtain the remote player spells object
            var playerSpells = playerObject.FindGameObjectInChildren("Spells");

            // Gather a bunch of object from the FSM state machine by indexing them
            // according to the parameters given to this function
            // They are different depending on which level of the Fireball spell we need to create
            var spellControl = HeroController.instance.spellControl;
            var fireballParent = spellControl.GetAction<SpawnObjectFromGlobalPool>(fireballParentName, 3).gameObject.Value;
            var fireballCast = fireballParent.LocateMyFSM("Fireball Cast");
            var audioAction = fireballCast.GetAction<AudioPlayerOneShotSingle>("Cast Right", castAudioIndex);
            var audioPlayerObj = audioAction.audioPlayer.Value;

            // Store the audio clip, each variation (flukenest, flukenest+defender crest, normal)
            // has an audio clip to play
            AudioClip castClip;
            if (hasFlukenestCharm) {
                // The audio clip for a variation containing flukenest is
                // always the one in the "Fluke R" state of the FSM
                castClip = (AudioClip) fireballCast.GetAction<AudioPlayerOneShotSingle>("Fluke R", 0).audioClip.Value;
                if (hasDefenderCrestCharm) {
                    var dungFlukeObj = fireballCast.GetAction<SpawnObjectFromGlobalPool>("Dung R", dungFlukeIndex)
                        .gameObject.Value;
                    // Instantiate the dungFluke object from the prefab obtained above
                    var dungFluke = Object.Instantiate(dungFlukeObj, playerSpells.transform.position,
                        Quaternion.identity);
                    dungFluke.SetActive(true);

                    // Make sure the object is scaled according to which direction the player is facing
                    var localScale = playerObject.transform.localScale;
                    dungFluke.transform.rotation = Quaternion.Euler(0, 0, 26 * -localScale.x);
                    dungFluke.layer = 22;
                    
                    // Get the control FSM and the audio clip corresponding to the explosion of the dungFluke
                    // We need it later
                    var dungFlukeControl = dungFluke.LocateMyFSM("Control");
                    var blowClip = (AudioClip) dungFlukeControl.GetAction<AudioPlayerOneShotSingle>("Blow", dungFlukeAudioIndex).audioClip.Value;
                    Object.Destroy(dungFlukeControl);
                    
                    // TODO: deal with PvP scenarios
                    
                    // Start a coroutine, because we need to do some waiting in here
                    MonoBehaviourUtil.Instance.StartCoroutine(StartDungFluke(dungFluke, blowClip));

                    // Create randomized x and y velocity, similar to the FSM state machine 
                    dungFluke.GetComponent<Rigidbody2D>().velocity = new Vector2(
                        Random.Range(5, 15) * -localScale.x, 
                        Random.Range(0, 20)
                    );
                    
                    Object.Destroy(dungFluke.FindGameObjectInChildren("Damager"));
                } else {
                    // Obtain the prefab and instantiate it for the fluke only variation
                    var flukeObject = fireballCast.GetAction<FlingObjectsFromGlobalPool>("Flukes", 0).gameObject.Value;
                    var fluke = Object.Instantiate(flukeObject, playerSpells.transform.position, Quaternion.identity);
                    
                    // TODO: deal with PvP scenarios 

                    // Create a config of how to fling the individual flukes
                    // based on the direction the player is facing
                    var localScale = playerObject.transform.localScale;
                    // This is all from the FSM
                    var config = new FlingUtils.Config {
                        Prefab = fluke,
                        AmountMin = 16,
                        AmountMax = 16,
                        AngleMin = localScale.x < 0 ? 20 : 120,
                        AngleMax = localScale.x < 0 ? 60 : 160,
                        SpeedMin = 14,
                        SpeedMax = 22
                    };
                    
                    // Spawn the flukes relative to the player object with the created config
                    FlingUtils.SpawnAndFling(config, playerObject.transform, Vector3.zero);
                }
            } else {
                // We already had a variable for the actual fireball state containing the correct audio clip
                castClip = (AudioClip) audioAction.audioClip.Value;
                
                // Get the prefab and instantiate it
                var fireballObject = fireballCast.GetAction<SpawnObjectFromGlobalPool>("Cast Right", castFireballIndex).gameObject.Value;
                var fireball = Object.Instantiate(fireballObject, playerSpells.transform.position + Vector3.down * 0.5f, Quaternion.identity);
                fireball.SetActive(true);
                fireball.layer = 22;

                // We add a fireball component that deals with spawning the moving fireball
                var fireballComponent = fireball.AddComponent<Fireball>();
                fireballComponent.xDir = -playerObject.transform.localScale.x;

                // Pass the relevant data to the fireball component
                fireballComponent.hasShamanStoneCharm = hasShamanStoneCharm;
                fireballComponent.baseFireballSize = baseFireballSize;
                fireballComponent.noFireballFlip = noFireballFlip;
            }
            
            // Play the audio clip corresponding to which variation we spawned
            var audioPlayer = audioPlayerObj.Spawn(playerObject.transform);
            audioPlayer.GetComponent<AudioSource>().PlayOneShot(castClip);
        }

        public void PreparePacket(Packet packet) {
            var playerData = PlayerData.instance;
            // Write charm values to the packet
            packet.Write(playerData.equippedCharm_11); // Flukenest
            packet.Write(playerData.equippedCharm_10); // Defender's Crest
            packet.Write(playerData.equippedCharm_19); // Shaman Stone
        }

        private IEnumerator StartDungFluke(GameObject dungFluke, AudioClip blowClip) {
            var spriteAnimator = dungFluke.GetComponent<tk2dSpriteAnimator>();
            var audioSource = dungFluke.GetComponent<AudioSource>();

            // Play the animation for the dungFluke movement and the corresponding audio
            spriteAnimator.Play("Dung Air");
            audioSource.Play();

            yield return new WaitForSeconds(1.0f);
            
            // Play the erratic movement animation just before it explodes
            spriteAnimator.Play("Dung Antic");
            dungFluke.FindGameObjectInChildren("Pt Antic").GetComponent<ParticleSystem>().Play();

            yield return new WaitForSeconds(1.0f);

            // Now we get the prefab and spawn the actual explosion cloud
            var dungCloudObject = dungFluke.FindGameObjectInChildren("Knight Dung Cloud");
            var dungCloud = Object.Instantiate(
                dungCloudObject,
                dungFluke.transform.position,
                Quaternion.identity
            );
            
            dungCloud.SetActive(true);
            dungCloud.layer = 22;
            
            Object.Destroy(dungCloud.GetComponent<DamageEffectTicker>());

            // Set the FSM state to Collider On, so we can actually interact with it
            dungCloud.LocateMyFSM("Control").SetState("Collider On");
            // Play the explosion audio clip
            dungCloud.AddComponent<AudioSource>().PlayOneShot(blowClip);
            
            // TODO: deal with PvP scenarios

            // We can already destroy the fluke
            Object.Destroy(dungFluke);

            yield return new WaitForSeconds(3.0f);

            // After some time, we can also destroy the cloud
            Object.Destroy(dungCloud);
        }
    }

    public class Fireball : MonoBehaviour {
        public float xDir;
        public bool hasShamanStoneCharm;
        public float baseFireballSize;
        public bool noFireballFlip;
        
        private const float FireballSpeed = 45;
        
        private tk2dSpriteAnimator _anim;
        private Rigidbody2D _rb;

        private void Awake() {
            _anim = GetComponent<tk2dSpriteAnimator>();
            _rb = GetComponent<Rigidbody2D>();
        }

        private void Start() {
            // Start playing the animation from the first frame
            _anim.PlayFromFrame(0);
            // Based on which direction the knight is facing, we set the velocity
            _rb.velocity = Vector2.right * FireballSpeed * xDir;
            
            // TODO: deal with PvP scenarios

            // For some reason, the FSM in the level 1 fireball flips the object
            // manually more times than the level 2 fireball, so we skip the flip
            if (noFireballFlip) {
                xDir = 1;
            }
            
            // Upscale the fireball if we have shaman stone equipped
            if (hasShamanStoneCharm) {
                transform.localScale = new Vector3(xDir * baseFireballSize * 1.3f, baseFireballSize * 1.6f, 0);
            } else {
                transform.localScale = new Vector3(xDir * baseFireballSize, baseFireballSize, 0);
            }

            // Destroy it after some time
            Destroy(gameObject, 2);
        }
    }
}