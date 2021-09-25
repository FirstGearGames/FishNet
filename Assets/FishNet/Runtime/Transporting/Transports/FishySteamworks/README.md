# FishySteamworks

	This is an improved fork from https://github.com/Chykary/FizzySteamworks 



## Dependencies
	.Net 4.5x

	If you already have SteamWorks.Net in your project, you might need to delete either your copy, or the one included within this transport.	

	These projects need to be installed and working before you can use this transport.
		1. **[SteamWorks.NET](https://github.com/rlabrecque/Steamworks.NET)** FishySteamworks relies on Steamworks.NET to communicate with the Steamworks API(https://partner.steamgames.com/doc/sdk).



## Setting Up

3. Add FishySteamworks component to your NetworkManager object. Either remove other transports or add TransportManager and specify which transport to use.

4. Enter your Steam App ID in the added FishySteamworks component.



## Host
To be able to have your game working you need to make sure you have Steam running in the background. **SteamManager will print a Debug Message if it initializes correctly.**



## Client
Before sending your game to your buddy make sure you have your **steamID64** ready. To find your **steamID64** the transport prints the hosts **steamID64** in the console when the server has started.

1. Send the game to your buddy. The transport shows your Steam User ID after you have started a server.
2. Your buddy needs your **steamID64** to be able to connect.
3. Place the **steamID64** into **"localhost"** then click **"Client"**
5. Then they will be connected to you.



## Testing your game locally
You cant connect to yourself locally while using FishySteamworks since it's using steams P2P. If you want to test your game locally you'll have to use default transport instead.
