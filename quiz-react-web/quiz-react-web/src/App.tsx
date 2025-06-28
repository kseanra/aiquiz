import React, { useState } from 'react';
import * as signalR from '@microsoft/signalr';

const HUB_URL = "https://localhost:5001/quizhub"; // Change if your API uses a different port

function App() {
  const [name, setName] = useState('');
  const [connected, setConnected] = useState(false);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [lastPong, setLastPong] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [question, setQuestion] = useState<string | null>(null); // New state for question
  const [answer, setAnswer] = useState(''); // New state for answer input
  const [playerStates, setPlayerStates] = useState<any[]>([]); // State for player statuses

  // Map status code to string
  const statusToString = (status: any) => {
    switch (status) {
      case 0:
      case 'JustJoined':
        return 'Just Joined';
      case 1:
      case 'Active':
        return 'Active';
      case 2:
      case 'Disconnected':
        return 'Disconnected';
      case 3:
      case 'ReadyForGame':
        return 'Ready for Game';
      case 4:
      case 'GameOver':
        return 'Game Over';
      case 5:
      case 'WaitingForGame':
        return 'Waiting for Game';
      case 6:
      case 'GameWinner':
        return 'Game Winner';
      default:
        return String(status);
    }
  };

  const handleConnect = async () => {
    if (!name) return;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .build();

    conn.on("ReceiveQuestion", (question: string) => {
      setQuestion(question); // Set question in state
    });

    conn.on("PlayersStatus", (players: any[]) => {
      setPlayerStates(players);
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
      await connection.invoke("ReadyForGame", true);
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
            {/* Player Status List */}
            {playerStates.length > 0 && (
              <div style={{ margin: '16px 0', padding: 12, background: '#e6f7ff', borderRadius: 8 }}>
                <strong>Player Status:</strong>
                <ul style={{ margin: 0, paddingLeft: 20 }}>
                  {playerStates.map((p, idx) => (
                    <li key={p.connectionId || idx}>
                      {p.name || 'Anonymous'}: {statusToString(p.status)} (Question #{(p.currentQuestionIndex ?? 0) + 1})
                    </li>
                  ))}
                </ul>
              </div>
            )}
            <button onClick={handleReady} disabled={ready} style={{ marginTop: 16 }}>
              {ready ? 'Ready!' : 'I am Ready'}
            </button>
            {question && (
              <div style={{ marginTop: 24, padding: 16, background: '#f9f9f9', borderRadius: 8 }}>
                <strong>Question:</strong> {question}
                <form
                  onSubmit={async e => {
                    e.preventDefault();
                    if (connection && answer.trim()) {
                      await connection.invoke("SubmitAnswer", answer);
                      setAnswer('');
                    }
                  }}
                  style={{ marginTop: 16 }}
                >
                  <input
                    type="text"
                    value={answer}
                    onChange={e => setAnswer(e.target.value)}
                    placeholder="Your answer"
                    style={{ marginRight: 8 }}
                  />
                  <button type="submit" disabled={!answer.trim()}>
                    Submit Answer
                  </button>
                </form>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default App;