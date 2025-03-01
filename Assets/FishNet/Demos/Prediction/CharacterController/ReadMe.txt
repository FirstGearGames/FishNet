This demo shows how to use a character controller with our prediction system.

Important:
    * Beta ReplicateStates must be enabled to use this demo. You can toggle this setting using the Fish-Networking menu. 

Setup:
	* Open demo scene.
	* Start server, host, or client only. You may test with parrelSync or host the project.

Notes:
    * Server and all clients run the same inputs. This is done by using State Forwarding on the NetworkObject inspector.
    * NetworkTrigger is used to attach to platforms. You may review code within the controller script to see attach logic and comments.
            On the prefab the trigger is attached as a child of the NetworkObject for sorting, but not under the graphical, as we do
            not want the trigger to be modified outside the tick system (graphical gets smoothed, and is updated outside the tick loop).
    * Moving platform predicts fully into the future so the client may step on it in real-time. See notes in MovingPlatform script.
            Spectated objects may appear to correct when moving onto the platform. This can be resolved by balancing the future
            prediction amount on the platform and the spectated objects. 
