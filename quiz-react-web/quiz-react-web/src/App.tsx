import React, { useState } from 'react';
import * as signalR from '@microsoft/signalr';

const HUB_URL = "https://localhost:5001/quizhub"; // Change if your API uses a different port

function App() {
  const [name, setName] = useState('');
  const [connected, setConnected] = useState(false);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [lastPong, setLastPong] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  const handleConnect = async () => {
    if (!name) return;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .build();

    conn.on("ReceiveQuestion", (question: string) => {
      alert("Received question: " + question);
    });

    conn.on("Pong", (serverTime: string) => {
      setLastPong(serverTime);
    });

    try {
      await conn.start();
      await conn.invoke("SubmitName", name);
      setConnection(conn);
      setConnected(true);
      setReady(false);
      // Start pinging every 1 second
      setInterval(() => {
        conn.invoke("Ping");
      }, 1000);
    } catch (error) {
      console.error("Connection failed: ", error);
      alert("Failed to connect. Please try again.");
      setConnected(false);
      setConnection(null);
    }
  };

  const handleReady = async () => {
    if (connection) {
      await connection.invoke("ReadyForGame");
      setReady(true);
    }
  };

  return (
    <div style={{ padding: 40 }}>
      {lastPong && (
        <div style={{ position: 'absolute', top: 0, left: 0, width: '100%', background: '#eee', padding: 8, textAlign: 'center' }}>
          Last Pong from server: {lastPong}
        </div>
      )}
      <div style={{ marginTop: lastPong ? 40 : 0 }}>
        {!connected ? (
          <form
            onSubmit={e => {
              e.preventDefault();
              handleConnect();
            }}
          >
            <label>
              Enter your name:
              <input
                value={name}
                onChange={e => setName(e.target.value)}
                style={{ marginLeft: 8 }}
                autoFocus
              />
            </label>
            <button type="submit" style={{ marginLeft: 8 }}>Enter</button>
          </form>
        ) : (
          <div>
            <h2>Welcome, {name}! Connected to the game room.</h2>
            <button onClick={handleReady} disabled={ready} style={{ marginTop: 16 }}>
              {ready ? 'Ready!' : 'I am Ready'}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export default App;