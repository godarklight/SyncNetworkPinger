using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SyncServer
{
	public class Server
	{
		private const int SERVER_PORT = 9001;

		public static void Main ()
		{
			Console.WriteLine ("Press enter to exit");
			SetupServer ();
			Console.ReadLine ();
		}

		public static void SetupServer ()
		{
			//Setup TCP
			TcpListener tcpServer = new TcpListener (new IPEndPoint (IPAddress.Any, SERVER_PORT));
			tcpServer.Start ();
			TcpServerObject tcpState = new TcpServerObject ();
			tcpState.server = tcpServer;
			tcpState.server.BeginAcceptTcpClient (new AsyncCallback (NewTCPClient), tcpState);
			//Setup UDP
			UdpClient udpServer = new UdpClient (new IPEndPoint (IPAddress.Any, SERVER_PORT));
			UdpServerObject udpState = new UdpServerObject ();
			udpState.server = udpServer;
			udpState.server.BeginReceive (new AsyncCallback (NewUDPData), udpState);
		}

		public static void NewTCPClient (IAsyncResult ar)
		{
			TcpServerObject tcpState = (TcpServerObject)ar.AsyncState;
			try {
				TcpClient newClient = tcpState.server.EndAcceptTcpClient (ar);
				newClient.NoDelay = true;
				TcpClientObject newClientObject = new TcpClientObject ();
				newClientObject.client = newClient;
				newClientObject.buffer = new byte[1024];
				newClientObject.client.GetStream ().BeginRead (newClientObject.buffer, 0, newClientObject.buffer.Length, new AsyncCallback (NewTCPData), newClientObject);
			} catch (Exception e) {
				Console.WriteLine ("Error accepting client: " + e.Message);
			}
			tcpState.server.BeginAcceptTcpClient (new AsyncCallback (NewTCPClient), tcpState);
		}

		public static void NewTCPData (IAsyncResult ar)
		{
			try {
				TcpClientObject tcpState = (TcpClientObject)ar.AsyncState;
				int bytes_read = tcpState.client.GetStream ().EndRead (ar);
				if (bytes_read > 0) {
					tcpState.client.GetStream ().Write (tcpState.buffer, 0, bytes_read);
				}
				tcpState.buffer = new byte[1024];
				tcpState.client.GetStream ().BeginRead (tcpState.buffer, 0, tcpState.buffer.Length, new AsyncCallback (NewTCPData), tcpState);
			} catch (Exception e) {
				Console.WriteLine ("Error relaying data: " + e.Message);
			}
		}

		public static void NewUDPData (IAsyncResult ar)
		{
			UdpServerObject udpState = (UdpServerObject)ar.AsyncState;
			IPEndPoint endpoint = new IPEndPoint (IPAddress.Any, SERVER_PORT);
			byte[] newData = udpState.server.EndReceive (ar, ref endpoint);
			udpState.server.Send (newData, newData.Length, endpoint);
			udpState.server.BeginReceive (new AsyncCallback (NewUDPData), udpState);
		}
		//Async State Objects
		public class TcpServerObject
		{
			public TcpListener server;
		}

		public class TcpClientObject
		{
			public TcpClient client;
			public byte[] buffer;
		}

		public class UdpServerObject
		{
			public UdpClient server;
		}
	}
}
