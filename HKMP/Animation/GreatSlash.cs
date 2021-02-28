﻿using HKMP.Networking.Packet;
using HutongGames.PlayMaker.Actions;
using ModCommon;
using ModCommon.Util;
using UnityEngine;

namespace HKMP.Animation {
    public class GreatSlash : IAnimationEffect {
        public void Play(GameObject playerObject, Packet packet) {
            // Obtain the Nail Arts FSM from the Hero Controller
            var nailArts = HeroController.instance.gameObject.LocateMyFSM("Nail Arts");
            
            // Obtain the AudioSource from the AudioPlayerOneShotSingle action in the nail arts FSM
            var audioAction = nailArts.GetAction<AudioPlayerOneShotSingle>("Play Audio", 0);
            var audioPlayerObj = audioAction.audioPlayer.Value;
            var audioPlayer = audioPlayerObj.Spawn(playerObject.transform);
            var audioSource = audioPlayer.GetComponent<AudioSource>();
            
            // Get the audio clip of the Great Slash
            var greatSlashClip = (AudioClip) nailArts.GetAction<AudioPlay>("G Slash", 0).oneShotClip.Value;
            audioSource.PlayOneShot(greatSlashClip);
                    
            // Get the attacks gameObject from the player object
            var localPlayerAttacks = HeroController.instance.gameObject.FindGameObjectInChildren("Attacks");
            var playerAttacks = playerObject.FindGameObjectInChildren("Attacks");
            
            // Get the prefab for the Great Slash and instantiate it relative to the remote player object
            var greatSlashObject = localPlayerAttacks.FindGameObjectInChildren("Great Slash");
            var greatSlash = Object.Instantiate(
                greatSlashObject,
                playerAttacks.transform
            );
            greatSlash.SetActive(true);
            greatSlash.layer = 22;

            // Set the newly instantiate collider to state Init, to reset it
            // in case the local player was already performing it
            greatSlash.LocateMyFSM("Control Collider").SetState("Init");

            // Get the animator, figure out the duration of the animation and destroy the object accordingly afterwards
            var greatSlashAnimator = greatSlash.GetComponent<tk2dSpriteAnimator>();
            var greatSlashAnimationDuration = greatSlashAnimator.DefaultClip.frames.Length / greatSlashAnimator.ClipFps;
            Object.Destroy(greatSlash, greatSlashAnimationDuration);
                    
            // TODO: deal with PvP scenarios
        }

        public void PreparePacket(Packet packet) {
        }
    }
}