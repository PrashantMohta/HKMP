using System;
using System.Collections.Generic;
using Hkmp.Networking.Packet.Data;
using Hkmp.Util;

namespace Hkmp.Networking.Packet {
    public delegate void ClientPacketHandler(IPacketData packet);

    /// <summary>
    /// A generic client packet handler delegate that has a IPacketData implementation as parameter.
    /// </summary>
    /// <typeparam name="TPacketData">The type of the packet data that is passed as parameter.</typeparam>
    public delegate void GenericClientPacketHandler<in TPacketData>(TPacketData packet) where TPacketData : IPacketData;

    /// <summary>
    /// Packet handler that only has the client ID as parameter and does not use the packet data.
    /// </summary>
    public delegate void EmptyServerPacketHandler(ushort id);

    /// <summary>
    /// Packet handler for the server that has the client ID and packet data as parameters.
    /// </summary>
    public delegate void ServerPacketHandler(ushort id, IPacketData packet);

    /// <summary>
    /// A generic server packet handler delegate that has a IPacketData implementation and client ID as parameter.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public delegate void GenericServerPacketHandler<in T>(ushort id, T packet) where T : IPacketData;

    /// <summary>
    /// Manages packets that are received by the given NetClient.
    /// </summary>
    public class PacketManager {
        /// <summary>
        /// Handlers that deal with data from the server intended for the client.
        /// </summary>
        private readonly Dictionary<ClientPacketId, ClientPacketHandler> _clientPacketHandlers;

        /// <summary>
        /// Handlers that deal with data from the client intended for the server.
        /// </summary>
        private readonly Dictionary<ServerPacketId, ServerPacketHandler> _serverPacketHandlers;

        /// <summary>
        /// Handlers that deal with client addon data from the server intended for the client.
        /// </summary>
        private readonly Dictionary<byte, Dictionary<byte, ClientPacketHandler>> _clientAddonPacketHandlers;

        /// <summary>
        /// Handlers that deal with server addon data from a client intended for the server.
        /// </summary>
        private readonly Dictionary<byte, Dictionary<byte, ServerPacketHandler>> _serverAddonPacketHandlers;

        public PacketManager() {
            _clientPacketHandlers = new Dictionary<ClientPacketId, ClientPacketHandler>();
            _serverPacketHandlers = new Dictionary<ServerPacketId, ServerPacketHandler>();

            _clientAddonPacketHandlers = new Dictionary<byte, Dictionary<byte, ClientPacketHandler>>();
            _serverAddonPacketHandlers = new Dictionary<byte, Dictionary<byte, ServerPacketHandler>>();
        }

        #region Client-related packet handling

        /**
         * Handle data received by a client.
         */
        public void HandleClientPacket(ClientUpdatePacket packet) {
            // Execute corresponding packet handlers for normal packet data
            UnpackPacketDataDict(packet.GetPacketData(), ExecuteClientPacketHandler);

            // Execute corresponding packet handlers for addon packet data of each addon in the packet
            foreach (var idPacketDataPair in packet.GetAddonPacketData()) {
                var addonId = idPacketDataPair.Key;
                var packetDataDict = idPacketDataPair.Value.PacketData;

                UnpackPacketDataDict(
                    packetDataDict,
                    (packetId, packetData) => ExecuteClientAddonPacketHandler(addonId, packetId, packetData)
                );
            }
        }

        /**
         * Executes the correct packet handler corresponding to this packet.
         * Assumes that the packet is not read yet.
         */
        private void ExecuteClientPacketHandler(ClientPacketId packetId, IPacketData packetData) {
            if (!_clientPacketHandlers.ContainsKey(packetId)) {
                Logger.Get().Warn(this, $"There is no client packet handler registered for ID: {packetId}");
                return;
            }

            // Invoke the packet handler for this ID on the Unity main thread
            ThreadUtil.RunActionOnMainThread(() => {
                try {
                    _clientPacketHandlers[packetId].Invoke(packetData);
                } catch (Exception e) {
                    Logger.Get().Error(this,
                        $"Exception occured while executing client packet handler for packet ID: {packetId}, message: {e.Message}, stacktrace: {e.StackTrace}");
                }
            });
        }

        private void RegisterClientPacketHandler(
            ClientPacketId packetId,
            ClientPacketHandler handler
        ) {
            if (_clientPacketHandlers.ContainsKey(packetId)) {
                Logger.Get().Error(this, $"Tried to register already existing client packet handler: {packetId}");
                return;
            }

            _clientPacketHandlers[packetId] = handler;
        }

        public void RegisterClientPacketHandler(
            ClientPacketId packetId,
            Action handler
        ) => RegisterClientPacketHandler(packetId, _ => handler());

        public void RegisterClientPacketHandler<T>(
            ClientPacketId packetId,
            GenericClientPacketHandler<T> handler
        ) where T : IPacketData => RegisterClientPacketHandler(packetId, iPacket => handler((T)iPacket));

        public void DeregisterClientPacketHandler(ClientPacketId packetId) {
            if (!_clientPacketHandlers.ContainsKey(packetId)) {
                Logger.Get().Error(this, $"Tried to remove nonexistent client packet handler: {packetId}");
                return;
            }

            _clientPacketHandlers.Remove(packetId);
        }

        #endregion

        #region Server-related packet handling

        /**
         * Handle data received by the server.
         */
        public void HandleServerPacket(ushort id, ServerUpdatePacket packet) {
            // Execute corresponding packet handlers
            UnpackPacketDataDict(
                packet.GetPacketData(),
                (packetId, packetData) => ExecuteServerPacketHandler(id, packetId, packetData)
            );
            
            // Execute corresponding packet handler for addon packet data of each addon in the packet
            foreach (var idPacketDataPair in packet.GetAddonPacketData()) {
                var addonId = idPacketDataPair.Key;
                var packetDataDict = idPacketDataPair.Value.PacketData;

                UnpackPacketDataDict(
                    packetDataDict,
                    (packetId, packetData) => ExecuteServerAddonPacketHandler(
                        id,
                        addonId,
                        packetId,
                        packetData
                    )
                );
            }
        }

        /**
         * Executes the correct packet handler corresponding to this packet.
         * Assumes that the packet is not read yet.
         */
        private void ExecuteServerPacketHandler(ushort id, ServerPacketId packetId, IPacketData packetData) {
            if (!_serverPacketHandlers.ContainsKey(packetId)) {
                Logger.Get().Warn(this, $"There is no server packet handler registered for ID: {packetId}");
                return;
            }

            // Invoke the packet handler for this ID directly, in contrast to the client packet handling.
            // We don't do anything game specific with server packet handler, so there's no need to do it
            // on the Unity main thread
            try {
                _serverPacketHandlers[packetId].Invoke(id, packetData);
            } catch (Exception e) {
                Logger.Get().Error(this,
                    $"Exception occured while executing server packet handler for packet ID: {packetId}, message: {e.Message}, stacktrace: {e.StackTrace}");
            }
        }

        private void RegisterServerPacketHandler(ServerPacketId packetId, ServerPacketHandler handler) {
            if (_serverPacketHandlers.ContainsKey(packetId)) {
                Logger.Get().Error(this, $"Tried to register already existing client packet handler: {packetId}");
                return;
            }

            _serverPacketHandlers[packetId] = handler;
        }

        public void RegisterServerPacketHandler(
            ServerPacketId packetId,
            EmptyServerPacketHandler handler
        ) => RegisterServerPacketHandler(packetId, (id, _) => handler(id));

        public void RegisterServerPacketHandler<T>(
            ServerPacketId packetId,
            GenericServerPacketHandler<T> handler
        ) where T : IPacketData => RegisterServerPacketHandler(
            packetId,
            (id, iPacket) => handler(id, (T)iPacket)
        );

        public void DeregisterServerPacketHandler(ServerPacketId packetId) {
            if (!_serverPacketHandlers.ContainsKey(packetId)) {
                Logger.Get().Error(this, $"Tried to remove nonexistent server packet handler: {packetId}");
                return;
            }

            _serverPacketHandlers.Remove(packetId);
        }

        #endregion

        #region Client-addon-related packet handling

        private void ExecuteClientAddonPacketHandler(
            byte addonId,
            byte packetId,
            IPacketData packetData
        ) {
            var addonPacketIdMessage = $"for addon ID: {addonId} and packet ID: {packetId}";
            var noHandlerWarningMessage =
                $"There is no client addon packet handler registered {addonPacketIdMessage}";
            if (!_clientAddonPacketHandlers.TryGetValue(addonId, out var addonPacketHandlers)) {
                Logger.Get().Warn(this, noHandlerWarningMessage);
                return;
            }

            if (!addonPacketHandlers.TryGetValue(packetId, out var handler)) {
                Logger.Get().Warn(this, noHandlerWarningMessage);
                return;
            }

            // Invoke the packet handler on the Unity main thread
            ThreadUtil.RunActionOnMainThread(() => {
                try {
                    handler.Invoke(packetData);
                } catch (Exception e) {
                    Logger.Get().Error(this,
                        $"Exception occurred while executing client addon packet handler {addonPacketIdMessage}, type: {e.GetType()}, message: {e.Message}, stacktrace: {e.StackTrace}");
                }
            });
        }

        public void RegisterClientAddonPacketHandler(
            byte addonId,
            byte packetId,
            ClientPacketHandler handler
        ) {
            if (!_clientAddonPacketHandlers.TryGetValue(addonId, out var addonPacketHandlers)) {
                addonPacketHandlers = new Dictionary<byte, ClientPacketHandler>();

                _clientAddonPacketHandlers[addonId] = addonPacketHandlers;
            }

            if (addonPacketHandlers.ContainsKey(packetId)) {
                throw new InvalidOperationException("There is already a packet handler for the given ID");
            }

            addonPacketHandlers[packetId] = handler;
        }

        public void DeregisterClientAddonPacketHandler(byte addonId, byte packetId) {
            const string invalidOperationExceptionMessage = "Could not remove nonexistent addon packet handler";

            if (!_clientAddonPacketHandlers.TryGetValue(addonId, out var addonPacketHandlers)) {
                throw new InvalidOperationException(invalidOperationExceptionMessage);
            }

            if (!addonPacketHandlers.ContainsKey(packetId)) {
                throw new InvalidOperationException(invalidOperationExceptionMessage);
            }

            addonPacketHandlers.Remove(packetId);
        }

        /// <summary>
        /// Clear all registered client addon packet handlers.
        /// </summary>
        public void ClearClientAddonPacketHandlers() {
            _clientAddonPacketHandlers.Clear();
        }

        #endregion

        #region Server-addon-related packet handling

        private void ExecuteServerAddonPacketHandler(
            ushort id,
            byte addonId,
            byte packetId,
            IPacketData packetData
        ) {
            var addonPacketIdMessage = $"for addon ID: {addonId} and packet ID: {packetId}";
            var noHandlerWarningMessage =
                $"There is no server addon packet handler registered {addonPacketIdMessage}";
            if (!_serverAddonPacketHandlers.TryGetValue(addonId, out var addonPacketHandlers)) {
                Logger.Get().Warn(this, noHandlerWarningMessage);
                return;
            }

            if (!addonPacketHandlers.TryGetValue(packetId, out var handler)) {
                Logger.Get().Warn(this, noHandlerWarningMessage);
                return;
            }

            // Invoke the packet handler for this ID directly, in contrast to the client packet handling.
            // We don't do anything game specific with server packet handler, so there's no need to do it
            // on the Unity main thread
            try {
                handler.Invoke(id, packetData);
            } catch (Exception e) {
                Logger.Get().Error(this,
                    $"Exception occurred while executing client addon packet handler {addonPacketIdMessage}, type: {e.GetType()}, message: {e.Message}, stacktrace: {e.StackTrace}");
            }
        }

        public void RegisterServerAddonPacketHandler(
            byte addonId,
            byte packetId,
            ServerPacketHandler handler
        ) {
            if (!_serverAddonPacketHandlers.TryGetValue(addonId, out var addonPacketHandlers)) {
                addonPacketHandlers = new Dictionary<byte, ServerPacketHandler>();

                _serverAddonPacketHandlers[addonId] = addonPacketHandlers;
            }

            if (addonPacketHandlers.ContainsKey(packetId)) {
                throw new InvalidOperationException("There is already a packet handler for the given ID");
            }

            addonPacketHandlers[packetId] = handler;
        }

        public void DeregisterServerAddonPacketHandler(byte addonId, byte packetId) {
            const string invalidOperationExceptionMessage = "Could not remove nonexistent addon packet handler";

            if (!_serverAddonPacketHandlers.TryGetValue(addonId, out var addonPacketHandlers)) {
                throw new InvalidOperationException(invalidOperationExceptionMessage);
            }

            if (!addonPacketHandlers.ContainsKey(packetId)) {
                throw new InvalidOperationException(invalidOperationExceptionMessage);
            }

            addonPacketHandlers.Remove(packetId);
        }

        #endregion

        #region Packet handling utilities

        /**
         * Iterates over the given dictionary and executes the handler for each IPacketData instance
         * inside. This method will unpack packet data collections and execute the handler for each
         * instance in its collection.
         */
        private void UnpackPacketDataDict<T>(
            Dictionary<T, IPacketData> packetDataDict,
            Action<T, IPacketData> handler
        ) {
            foreach (var idPacketDataPair in packetDataDict) {
                var packetId = idPacketDataPair.Key;
                var packetData = idPacketDataPair.Value;

                // Check if this is a collection and if so, execute the handler for each instance in it
                if (packetData is RawPacketDataCollection rawPacketDataCollection) {
                    foreach (var dataInstance in rawPacketDataCollection.DataInstances) {
                        handler.Invoke(packetId, dataInstance);
                    }
                } else {
                    handler.Invoke(packetId, packetData);
                }
            }
        }

        public static List<Packet> HandleReceivedData(byte[] receivedData, ref byte[] leftoverData) {
            var currentData = receivedData;

            // Check whether we have leftover data from the previous read, and concatenate the two byte arrays
            if (leftoverData != null && leftoverData.Length > 0) {
                currentData = new byte[leftoverData.Length + receivedData.Length];

                // Copy over the leftover data into the current data array
                for (var i = 0; i < leftoverData.Length; i++) {
                    currentData[i] = leftoverData[i];
                }

                // Copy over the trimmed data into the current data array
                for (var i = 0; i < receivedData.Length; i++) {
                    currentData[leftoverData.Length + i] = receivedData[i];
                }

                leftoverData = null;
            }

            // Create packets from the data
            return ByteArrayToPackets(currentData, ref leftoverData);
        }

        private static List<Packet> ByteArrayToPackets(byte[] data, ref byte[] leftover) {
            var packets = new List<Packet>();

            // Keep track of current index in the data array
            var readIndex = 0;

            // The only break from this loop is when there is no new packet to be read
            do {
                // If there is still an int (4 bytes) to read in the data,
                // it represents the next packet's length
                var packetLength = 0;
                var unreadDataLength = data.Length - readIndex;
                if (unreadDataLength > 1) {
                    packetLength = BitConverter.ToUInt16(data, readIndex);
                    readIndex += 2;
                }

                // There is no new packet, so we can break
                if (packetLength <= 0) {
                    break;
                }

                // Check whether our given data array actually contains
                // the same number of bytes as the packet length
                if (data.Length - readIndex < packetLength) {
                    // There is not enough bytes in the data array to fill the requested packet with
                    // So we put everything, including the packet length ushort (2 bytes) into the leftover byte array
                    leftover = new byte[unreadDataLength];
                    for (var i = 0; i < unreadDataLength; i++) {
                        // Make sure to index data 2 bytes earlier, since we incremented
                        // when we read the packet length ushort
                        leftover[i] = data[readIndex - 2 + i];
                    }

                    break;
                }

                // Read the next packet's length in bytes
                var packetData = new byte[packetLength];
                for (var i = 0; i < packetLength; i++) {
                    packetData[i] = data[readIndex + i];
                }

                readIndex += packetLength;

                // Create a packet out of this byte array
                var newPacket = new Packet(packetData);

                // Add it to the list of parsed packets
                packets.Add(newPacket);
            } while (true);

            return packets;
        }

        #endregion
    }
}