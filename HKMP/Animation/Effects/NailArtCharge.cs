﻿using HKMP.Networking.Packet.Custom;
using HKMP.Util;
using ModCommon;
using UnityEngine;

namespace HKMP.Animation.Effects {
    public class NailArtCharge : AnimationEffect {
        public override void Play(GameObject playerObject, ClientPlayerAnimationUpdatePacket packet) {
            // Get the player attacks object
            var playerAttacks = playerObject.FindGameObjectInChildren("Attacks");
            
            // Create a new art charge object from the prefab in the hero controller
            // This is the soul-like particles that flow towards the player
            var artChargeObject = HeroController.instance.artChargeEffect;
            var artCharge = Object.Instantiate(
                artChargeObject,
                playerAttacks.transform
            );
            // Give it a name, so we can reference it when it needs to be destroyed
            artCharge.name = "Nail Art Charge";
            // Set is to active to start the animation
            artCharge.SetActive(true);

            // Get a new audio source object relative to the player object
            var artChargeAudioObject = AudioUtil.GetAudioSourceObject(playerAttacks);
            // Again give a name, so we can destroy it later
            artChargeAudioObject.name = "Nail Art Charge Audio";
            // Get the actual audio source
            var artChargeAudioSource = artChargeAudioObject.GetComponent<AudioSource>();
            
            // Get the nail art charge clip and play it
            var heroAudioController = HeroController.instance.GetComponent<HeroAudioController>();
            artChargeAudioSource.clip = heroAudioController.nailArtCharge.clip;
            artChargeAudioSource.Play();
        }

        public override void PreparePacket(ServerPlayerAnimationUpdatePacket packet) {
        }
    }
}