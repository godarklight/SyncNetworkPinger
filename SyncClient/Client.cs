using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SyncClient
{
	public class Client
	{
		private static int sequenceNumber = 0;
		private static bool isUDPConnected;
		private static bool isTCPConnected;
		private static TcpClientObject tcpConnection;
		private static UdpClientObject udpConnection;
		private static bool isRunning = true;
		private static string serverIP = "127.0.0.1";
		private const int SERVER_PORT = 9001;
		private const int PING_TIME = 1000;
		//Send a ping every second
		private static long startConnectionTime;

		public static void Main (string[] args)
		{
			if (args.Length == 1) {
				serverIP = args [0];
			}
			Console.Error.WriteLine ("Connecting to " + serverIP + "...");
			if (ConnectToServer ()) {
				Console.Error.WriteLine ("Connected.");
				tcpConnection.client.GetStream ().BeginRead (tcpConnection.buffer, tcpConnection.buffer.Length - tcpConnection.bytes_left, tcpConnection.bytes_left, new AsyncCallback (HandleTCPMessage), tcpConnection);
				udpConnection.client.BeginReceive (new AsyncCallback (HandleUDPMessage), udpConnection);
				Thread sendThread = new Thread (new ThreadStart (sendThreadMain));
				sendThread.Start ();
			} else {
				Console.Error.WriteLine ("Failed to connect.");
			}

			Console.Error.WriteLine ("Press enter to exit.");
			Console.ReadLine ();
			isRunning = false;
			tcpConnection.client.Close ();
			udpConnection.client.Close ();
		}

		public static bool ConnectToServer ()
		{
			IPAddress serverAddress;
			if (!IPAddress.TryParse (serverIP, out serverAddress)) {
				Console.Error.WriteLine ("Invalid IP: " + serverIP);
				return false;
			}
			TcpClient tcpClient = new TcpClient ();
			TcpClientObject tcpState = new TcpClientObject ();
			tcpState.client = tcpClient;
			tcpState.client.NoDelay = true;
			tcpState.buffer = new byte[12];
			tcpState.bytes_left = 12;
			UdpClient udpClient = new UdpClient ();
			UdpClientObject udpState = new UdpClientObject ();
			udpState.client = udpClient;
			startConnectionTime = DateTime.UtcNow.Ticks;
			tcpState.client.BeginConnect (serverAddress, SERVER_PORT, new AsyncCallback (TcpConnectCallback), tcpState);
			udpState.client.BeginReceive (new AsyncCallback (UdpConnectCallback), udpState);
			udpState.client.Connect (serverAddress, SERVER_PORT);
			udpState.client.Send (new byte[1], 1);
			int sleepCount = 0;
			//Wait until both TCP and UDP are connected.
			while (!isUDPConnected || !isTCPConnected) {
				Thread.Sleep (100);
				sleepCount++;
				//Max 10 seconds to connect
				if (sleepCount > 100) {
					return false;
				}
			}
			tcpConnection = tcpState;
			udpConnection = udpState;
			return true;
		}

		public static void TcpConnectCallback (IAsyncResult ar)
		{
			TcpClientObject tcpState = (TcpClientObject)ar.AsyncState;
			try {
				tcpState.client.EndConnect (ar);
				if (tcpState.client != null ? tcpState.client.Connected : false) {
					double deltaTime = Math.Round ((double)(DateTime.UtcNow.Ticks - startConnectionTime) / 10000, 4);
					Console.Error.WriteLine ("TCP Connected in " + deltaTime + "ms.");
					isTCPConnected = true;
				} else {
					Console.Error.WriteLine ("TCP failed to connect.");
				}
			} catch (Exception e) {
				Console.Error.WriteLine ("TCP failed to connect. Error: " + e.Message);
			}
		}

		public static void UdpConnectCallback (IAsyncResult ar)
		{
			UdpClientObject udpState = (UdpClientObject)ar.AsyncState;
			IPEndPoint endpoint = new IPEndPoint (IPAddress.Any, SERVER_PORT);
			byte[] newData = udpState.client.EndReceive (ar, ref endpoint);
			if (newData.Length == 1) {
				double deltaTime = Math.Round ((double)(DateTime.UtcNow.Ticks - startConnectionTime) / 10000, 4);
				Console.Error.WriteLine ("UDP Connected in " + deltaTime + "ms.");
				isUDPConnected = true;
			}
		}

		public static void sendTCPMessage (TcpClientObject tcpState)
		{
			byte[] sendData = new byte[12];
			BitConverter.GetBytes (sequenceNumber).CopyTo (sendData, 0);
			BitConverter.GetBytes (DateTime.UtcNow.Ticks).CopyTo (sendData, 4);
			try {
				tcpState.client.GetStream ().BeginWrite (sendData, 0, 12, new AsyncCallback (sendTCPMessageCallback), tcpState);
			} catch (Exception e) {
				Console.Error.WriteLine ("TCP Send Start Error: " + e.Message);
				isTCPConnected = false;
			}
		}

		public static void sendTCPMessageCallback (IAsyncResult ar)
		{
			TcpClientObject tcpState = (TcpClientObject)ar.AsyncState;
			try {
				tcpState.client.GetStream ().EndWrite (ar);
			} catch (Exception e) {
				Console.Error.WriteLine ("TCP Send End Error: " + e.Message);
				isTCPConnected = false;
			}
		}

		public static void sendUDPMessage (UdpClientObject udpState)
		{
			byte[] sendData = new byte[12];
			BitConverter.GetBytes (sequenceNumber).CopyTo (sendData, 0);
			BitConverter.GetBytes (DateTime.UtcNow.Ticks).CopyTo (sendData, 4);
			try {
				udpState.client.BeginSend (sendData, 12, new AsyncCallback (sendUDPMessageCallback), udpState);
			} catch (Exception e) {
				Console.Error.WriteLine ("UDP Send Start Error: " + e.Message);
				isUDPConnected = false;
			}
        
		}

		public static void sendUDPMessageCallback (IAsyncResult ar)
		{
			UdpClientObject udpState = (UdpClientObject)ar.AsyncState;
			try {
				udpState.client.EndSend (ar);
			} catch (Exception e) {
				Console.Error.WriteLine ("UDP Send End Error: " + e.Message);
				isUDPConnected = false;
			}
		}

		public static void HandleUDPMessage (IAsyncResult ar)
		{
			UdpClientObject udpState = (UdpClientObject)ar.AsyncState;
			try {
				IPEndPoint endpoint = new IPEndPoint (IPAddress.Any, SERVER_PORT);
				byte[] newData = udpState.client.EndReceive (ar, ref endpoint);
				if (newData.Length == 12) {
					int sequence = BitConverter.ToInt32 (newData, 0);
					long sendTime = BitConverter.ToInt64 (newData, 4);
					double deltaTime = Math.Round ((double)(DateTime.UtcNow.Ticks - sendTime) / 10000, 4);
					Console.WriteLine ("UDP " + sequence + " " + deltaTime);
				}
				udpConnection.client.BeginReceive (new AsyncCallback (HandleUDPMessage), udpConnection);
			} catch (Exception e) {
				Console.Error.WriteLine ("UDP Receive Error: " + e.Message);
			}
		}

		public static void HandleTCPMessage (IAsyncResult ar)
		{
			TcpClientObject tcpState = (TcpClientObject)ar.AsyncState;
			try {
				tcpState.bytes_left -= tcpState.client.GetStream ().EndRead (ar);
				if (tcpState.bytes_left == 0) {
					long sequence = BitConverter.ToInt32 (tcpState.buffer, 0);
					long sendTime = BitConverter.ToInt64 (tcpState.buffer, 4);
					double deltaTime = Math.Round ((double)(DateTime.UtcNow.Ticks - sendTime) / 10000, 4);
					Console.WriteLine ("TCP " + sequence + " " + deltaTime);
					tcpState.buffer = new byte[12];
					tcpState.bytes_left = 12;
				}
				tcpConnection.client.GetStream ().BeginRead (tcpConnection.buffer, tcpConnection.buffer.Length - tcpConnection.bytes_left, tcpConnection.bytes_left, new AsyncCallback (HandleTCPMessage), tcpConnection);
			} catch (Exception e) {
				Console.Error.WriteLine ("TCP Receive Error: " + e.Message);
			}
		}

		public static void sendThreadMain ()
		{
			while (isRunning && isTCPConnected && isUDPConnected) {
				sendTCPMessage (tcpConnection);
				sendUDPMessage (udpConnection);
				Thread.Sleep (1000);
				sequenceNumber++;
			}
		}
	}

	public class TcpClientObject
	{
		public TcpClient client;
		public byte[] buffer;
		public int bytes_left;
	}

	public class UdpClientObject
	{
		public UdpClient client;
	}
}
