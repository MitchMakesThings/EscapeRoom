using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class Network : Node
{
    public static Network Instance { get; private set; }

    [Signal]
    public delegate void ServerCreated();

	[Signal]
    public delegate void JoinSuccess();

	[Signal]
    public delegate void JoinFailed();

    [Signal]
    public delegate void PlayersChanged();  // Called AFTER player list has been updated

    public Dictionary<int, Player> Players = new Dictionary<int, Player>();

    public int MaxPlayers = 4;
    public static int Port { get; } = 4546;

    public override void _Ready()
    {
        base._Ready();

        Instance = this;

        // Connect to key networking signals
        GetTree().Connect("network_peer_connected", this, nameof(_on_player_connected));
        GetTree().Connect("network_peer_disconnected", this, nameof(_on_player_disconnected));
        GetTree().Connect("connected_to_server", this, nameof(_on_connected_to_server));
        GetTree().Connect("server_disconnected", this, nameof(_on_disconnected_from_server));
        GetTree().Connect("connection_failed", this, nameof(_on_connection_failed));
        Connect(nameof(JoinSuccess), this, nameof(_on_join_success));
        Connect(nameof(ServerCreated), this, nameof(_on_server_created));
    }

    public void CreateServer() {
        var net = new NetworkedMultiplayerENet();

        if (net.CreateServer(Port) != Error.Ok){
            GD.Print("Failed to create server");
            return;
        }

        GetTree().NetworkPeer = net;
        EmitSignal(nameof(ServerCreated));
    }

    public void JoinServer(string ip, int port) {
        var net = new NetworkedMultiplayerENet();

        if (net.CreateClient(ip, port) != Error.Ok) {
            GD.Print("Failed to connect to server");
            return;
        }

        GetTree().NetworkPeer = net;

        // Not emitting a signal here. JoinSuccess will be triggered when godots networking system tells us we're connected.
    }

    // All clients will call this on the server when they first connect
    [Remote]
    public void NetworkRegisterPlayer(Godot.Collections.Dictionary<string, object> encodedNewPlayer) {

        Player newPlayer = Player.PopulateFromGodotDictionary(encodedNewPlayer);

        // Get the unique identifier of the remote system calling this method
        int callerNetworkId = GetTree().GetRpcSenderId();
        GD.Print(nameof(NetworkRegisterPlayer), " called by Network ID: ", callerNetworkId);

        // Security check
        // If we're the server all clients will call this method when they first connect
        // If we're a client, this should only ever be called by the server
        // AKA, if another client is calling this method we want to ignore it
        if (!GetTree().IsNetworkServer() && callerNetworkId != 1) return;

        GD.Print("Made it past security check");
        
        // If we are the server we need to update all players (and send info about all players to the new player)
        if (GetTree().IsNetworkServer()) {
            // Store the network ID of the new player in the player object
            // TODO - it'd almost be nicer to have a list of players client-side.
            // If we're doing network-authoritative, there's no need for clients to know the id of other clients
            newPlayer.NetworkId = callerNetworkId;

            foreach (int playerId in Players.Keys) {
                // Tell the existing player about the new player
                // We don't do this for the server player, since we *are* the server!
                // We'll update our dictionary after the if we're currently in
                if (playerId != 1) {
                    RpcId(playerId, nameof(NetworkRegisterPlayer), newPlayer.ToGodotDictionary());
                }

                GD.Print("Sending info about player ", playerId, " to client ", callerNetworkId);

                // Tell the new player about the existing player
                // We don't do this for the server, because we're currently running that exact code!
                RpcId(callerNetworkId, nameof(NetworkRegisterPlayer), Players[playerId].ToGodotDictionary());
            }
        }

        RegisterPlayer(newPlayer.NetworkId, newPlayer);
    }

    private void RegisterPlayer(int networkId, Player newPlayer) {
        // This bit will run everywhere - server AND client
        Players.Add(networkId, newPlayer);

        GD.Print("RegisterPlayer:: Just registered information about ID ", networkId);
        GD.Print(newPlayer.Name);
        GD.Print(newPlayer.Color);
    }


    // Called (by Godot networking) on EVERYONE when a new player connects
    private void _on_player_connected(int id) {

    }

    // Called (by Godot networking) on EVERYONE when a player is disconnected
    private void _on_player_disconnected(int id) {

    }

    // Called (by Godot networking) on CLIENTS when they connect to the server
    private void _on_connected_to_server() {
        EmitSignal(nameof(JoinSuccess));
    }

    // Called on server from this class when the server is initialized
    private void _on_server_created() {
        // TODO populate player data
        RegisterPlayer(1, new Player() {
            Name = "ServerPlayer",
            NetworkId = 1
        });
    }

    // Called on clients from this class when we've joined a server
    // We're using this instead of the built-in godot signal to give us flexibility for steam etc in future with (hopefully) fewer changes
    private void _on_join_success() {
        GD.Print("_on_joined_success executing!");

        // TODO configurable Player object
        Players.Add(GetTree().GetNetworkUniqueId(), new Player() {
            Name = "ClientPlayer"
        });

        // Register ourselves with the server.
        // It'll then tell all the other players about us
        // Using .First() because I'm lazy, it's good to know about LINQ, and we're guaranteed to be the only one in the dictionary at this stage
        RpcId(1, nameof(NetworkRegisterPlayer), Players.Values.First().ToGodotDictionary());
    }

    // Called (by Godot networking) on client when it disconnects
    private void _on_disconnected_from_server() {
        GD.Print("The server abandoned me!");
    }

    // Called (by Godot networking) on client when connection attempt failed
    private void _on_connection_failed() {
        GetTree().NetworkPeer = null;
        EmitSignal(nameof(JoinFailed));
    }
}
