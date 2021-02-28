﻿using HKMP.Networking.Packet;
using HutongGames.PlayMaker.Actions;
using ModCommon;
using ModCommon.Util;
using UnityEngine;

namespace HKMP.Animation.Effects {
    public abstract class SlashBase : IAnimationEffect {
        public abstract void Play(GameObject playerObject, Packet packet);

        public void PreparePacket(Packet packet) {
            var playerData = PlayerData.instance;
            // Write health values to the packet
            packet.Write(playerData.health == 1);
            packet.Write(playerData.health == playerData.maxHealth);

            // Write charm values to the packet
            packet.Write(playerData.equippedCharm_6); // Fury of the fallen
            packet.Write(playerData.equippedCharm_13); // Mark of pride
            packet.Write(playerData.equippedCharm_18); // Long nail
            packet.Write(playerData.equippedCharm_35); // Grubberfly's Elegy
        }

        public void Play(GameObject playerObject, Packet packet, GameObject prefab, bool down, bool up, bool wall) {
            // Read all needed information to do this effect from the packet
            var isOnOneHealth = packet.ReadBool();
            var isOnFullHealth = packet.ReadBool();
            var hasFuryCharm = packet.ReadBool();
            var hasMarkOfPrideCharm = packet.ReadBool();
            var hasLongNailCharm = packet.ReadBool();
            var hasGrubberflyElegyCharm = packet.ReadBool();

            // Get the attacks gameObject from the player object
            var playerAttacks = playerObject.FindGameObjectInChildren("Attacks");

            // Instantiate the slash gameObject from the given prefab
            // and use the attack gameObject as transform reference
            var slash = Object.Instantiate(prefab, playerAttacks.transform);
            slash.SetActive(true);

            // Get the slash audio source and its clip
            var slashAudioSource = slash.GetComponent<AudioSource>();
            // Remove original audio source to prevent double audio
            Object.Destroy(slashAudioSource);
            var slashClip = slashAudioSource.clip;
            
            // Obtain the Nail Arts FSM from the Hero Controller
            var nailArts = HeroController.instance.gameObject.LocateMyFSM("Nail Arts");
            
            // Obtain the AudioSource from the AudioPlayerOneShotSingle action in the nail arts FSM
            var audioAction = nailArts.GetAction<AudioPlayerOneShotSingle>("Play Audio", 0);
            var audioPlayerObj = audioAction.audioPlayer.Value;
            var audioPlayer = audioPlayerObj.Spawn(playerObject.transform);
            var audioSource = audioPlayer.GetComponent<AudioSource>();
            
            // Play the slash clip with this newly spawned AudioSource
            audioSource.PlayOneShot(slashClip);

            // Store a boolean indicating whether the Fury of the fallen effect is active
            var fury = hasFuryCharm && isOnOneHealth;

            // Get the NailSlash component and set its values
            // based on the charms and fury state we have
            var nailSlash = slash.GetComponent<NailSlash>();
            nailSlash.SetLongnail(hasLongNailCharm);
            nailSlash.SetMantis(hasMarkOfPrideCharm);
            nailSlash.SetFury(fury);

            // If it is a wall slash, there is no scaling to do
            if (!wall) {
                // Scale the nail slash based on Long nail and Mark of pride charms
                if (hasLongNailCharm) {
                    if (hasMarkOfPrideCharm) {
                        nailSlash.transform.localScale = new Vector3(nailSlash.scale.x * 1.4f, nailSlash.scale.y * 1.4f,
                            nailSlash.scale.z);
                    } else {
                        nailSlash.transform.localScale = new Vector3(nailSlash.scale.x * 1.25f,
                            nailSlash.scale.y * 1.25f,
                            nailSlash.scale.z);
                    }
                } else if (hasMarkOfPrideCharm) {
                    nailSlash.transform.localScale = new Vector3(nailSlash.scale.x * 1.15f, nailSlash.scale.y * 1.15f,
                        nailSlash.scale.z);
                }
            }

            // Finally start the slash animation
            nailSlash.StartSlash();

            // TODO: deal with PvP scenarios


            if (!hasGrubberflyElegyCharm
                || isOnOneHealth && !hasFuryCharm
                || !isOnFullHealth) {
                return;
            }

            GameObject elegyBeamPrefab;

            // Store a boolean indicating that we should take the fury variant of the beam prefab
            var furyVariant = isOnOneHealth;
            if (down) {
                elegyBeamPrefab = furyVariant
                    ? HeroController.instance.grubberFlyBeamPrefabD_fury
                    : HeroController.instance.grubberFlyBeamPrefabD;
            } else if (up) {
                elegyBeamPrefab = furyVariant
                    ? HeroController.instance.grubberFlyBeamPrefabU_fury
                    :HeroController.instance.grubberFlyBeamPrefabU;
            } else {
                var facingLeft = playerObject.transform.localScale.x > 0;

                if (facingLeft) {
                    elegyBeamPrefab = furyVariant
                        ? HeroController.instance.grubberFlyBeamPrefabL_fury
                        : HeroController.instance.grubberFlyBeamPrefabL;
                } else {
                    elegyBeamPrefab = furyVariant
                        ? HeroController.instance.grubberFlyBeamPrefabR_fury
                        : HeroController.instance.grubberFlyBeamPrefabR;
                }
            }
            
            // Instantiate the beam from the prefab with the playerObject position
            var elegyBeam = Object.Instantiate(
                elegyBeamPrefab,
                playerObject.transform.position,
                Quaternion.identity
            );

            elegyBeam.SetActive(true);
            elegyBeam.layer = 22;

            // Rotate the beam if it is an up or down slash
            var localScale = elegyBeam.transform.localScale;
            if (up || down) {
                elegyBeam.transform.localScale = new Vector3(
                    playerObject.transform.localScale.x,
                    localScale.y,
                    localScale.z
                );
                var z = 90;
                if (down && playerObject.transform.localScale.x < 0) {
                    z = -90;
                }

                if (up && playerObject.transform.localScale.x > 0) {
                    z = -90;
                }

                elegyBeam.transform.rotation = Quaternion.Euler(
                    0,
                    0,
                    z
                );
            }

            Object.Destroy(elegyBeam.LocateMyFSM("damages_enemy"));

            // TODO: deal with PvP scenarios
            
            // We can destroy the slash and elegy beam objects after some time
            Object.Destroy(slash, 2.0f);
            Object.Destroy(elegyBeam, 2.0f);
        }
    }
}