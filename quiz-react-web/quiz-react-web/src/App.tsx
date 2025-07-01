import React, { useState } from 'react';
import * as signalR from '@microsoft/signalr';

const HUB_URL = "https://localhost:5001/quizhub"; // Change if your API uses a different port

function App() {
  const [name, setName] = useState('');
  const [connected, setConnected] = useState(false);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [lastPong, setLastPong] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [question, setQuestion] = useState<any | null>(null); // Now stores Quiz object
  const [selectedOption, setSelectedOption] = useState<string>(''); // For radio selection
  const [playerStates, setPlayerStates] = useState<any[]>([]); // State for player statuses
  const [gameOver, setGameOver] = useState(false);
  const [winnerName, setWinnerName] = useState<string | null>(null);
  const [loadingQuestion, setLoadingQuestion] = useState(false);
  const [incorrectIndex, setIncorrectIndex] = useState<number | null>(null);
  const [showBigCross, setShowBigCross] = useState(false);
  const [blockAnswer, setBlockAnswer] = useState(false);
  const [showSetTopic, setShowSetTopic] = useState(false);
  const [topicInput, setTopicInput] = useState('');

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
      // .withAutomaticReconnect()
      .build();

    conn.on("ReceiveQuestion", (quiz: any) => {
      setQuestion(quiz); // Set Quiz object in state
      setLoadingQuestion(false);
    });

    conn.on("PlayersStatus", (players: any[]) => {
      setPlayerStates(players);
    });

    conn.on("Pong", (serverTime: string) => {
      setLastPong(serverTime);
    });

    conn.on("GameOver", (players: any[]) => {
      setGameOver(true);
      setQuestion(null);
      setSelectedOption('');
      setPlayerStates(players);
      setLoadingQuestion(false);
      // Find winner
      const winner = players.find((p: any) => p.status === 6 || p.status === 'GameWinner');
      setWinnerName(winner ? winner.name : null);
    });

    conn.on("IncorrectAnswer", (questionIndex: number) => {
        setIncorrectIndex(questionIndex);
        setShowBigCross(true);
        setLoadingQuestion(false);
        setBlockAnswer(true);
        setTimeout(() => {
          setShowBigCross(false);
          setBlockAnswer(false);
        }, 2000);
    });

    conn.on("RequestSetTopic", () => {
      setShowSetTopic(true);
    });

    try {
      await conn.start();
      await conn.invoke("SubmitName", name);
      setConnection(conn);
      setConnected(true);
      setReady(false);
      // Start pinging every 1 second
      setInterval(() => {
        if(!conn || conn.state !== signalR.HubConnectionState.Connected) return;
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
      setLoadingQuestion(true); // Show loading spinner when ready
      await connection.invoke("ReadyForGame", true);
      setReady(true);
    }
  };

  const handleExit = async () => {
    if (connection) {
      await connection.stop();
    }
    setConnected(false);
    setConnection(null);
    setReady(false);
    setQuestion(null);
    setSelectedOption('');
    setPlayerStates([]);
    setGameOver(false);
    setWinnerName(null);
    setName('');
  };

  return (
    <div style={{ padding: 40, position: 'relative' }}>
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
            {/* Hide Ready button after click */}
            {!ready && (
              <button onClick={handleReady} style={{ marginTop: 16 }}>
                I am Ready
              </button>
            )}
            {gameOver ? (
              <div style={{ marginTop: 24, padding: 16, background: '#ffecec', borderRadius: 8 }}>
                <strong>Game Over!</strong><br />
                {winnerName ? `Winner: ${winnerName}` : 'No winner.'}
                <div style={{ marginTop: 16 }}>
                  <button onClick={handleExit}>Exit</button>
                </div>
              </div>
            ) : question && (
              <div style={{ marginTop: 24, padding: 16, background: '#f9f9f9', borderRadius: 8 }}>
                <strong>Question:</strong> {question.question}
                <form
                  onSubmit={async e => {
                    e.preventDefault();
                    if (connection && selectedOption) {
                      setLoadingQuestion(true);
                      await connection.invoke("SubmitAnswer", selectedOption);
                      setSelectedOption('');
                      // Wait for next question or GameOver event
                    }
                  }}
                  style={{ marginTop: 16 }}
                >
                  <ul style={{ marginTop: 12, listStyle: 'none', padding: 0 }}>
                    {question.options && question.options.map((opt: string, idx: number) => (
                      <li key={idx} style={{ marginBottom: 8, display: 'flex', alignItems: 'center' }}>
                        <label style={{ display: 'flex', alignItems: 'center' }}>
                          <input
                            type="radio"
                            name="quizOption"
                            value={opt}
                            checked={selectedOption === opt}
                            onChange={() => {
                              if (!blockAnswer) {
                                setSelectedOption(opt);
                                setIncorrectIndex(null); // Clear cross when user changes selection
                              }
                            }}
                            style={{ marginRight: 8 }}
                            disabled={blockAnswer}
                          />
                          {opt}
                        </label>
                      </li>
                    ))}
                  </ul>
                  <button type="submit" disabled={!selectedOption || loadingQuestion || blockAnswer}>
                    Submit Answer
                  </button>
                </form>   
              </div>
            )}
            {loadingQuestion && (
                  <div style={{ marginTop: 16, textAlign: 'center' }}>
                    <span className="spinner" style={{ display: 'inline-block', width: 32, height: 32, border: '4px solid #ccc', borderTop: '4px solid #333', borderRadius: '50%', animation: 'spin 1s linear infinite' }} />
                    <div>Waiting for next question...</div>
                  </div>
                )}
          </div>
        )}
      </div>
      {showBigCross && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          width: '100vw',
          height: '100vh',
          background: 'rgba(255,255,255,0.7)',
          zIndex: 9999,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}>
          <span style={{ color: 'red', fontSize: 120, fontWeight: 'bold', textShadow: '2px 2px 8px #fff' }}>&#10060;</span>
        </div>
      )}
      {showSetTopic && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          width: '100vw',
          height: '100vh',
          background: 'rgba(255,255,255,0.8)',
          zIndex: 10000,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}>
          <form
            onSubmit={async e => {
              e.preventDefault();
              if (connection && topicInput.trim()) {
                await connection.invoke("SetQuizTopic", topicInput.trim());
                setShowSetTopic(false);
                setTopicInput('');
              }
            }}
            style={{ background: '#fff', padding: 32, borderRadius: 12, boxShadow: '0 2px 16px #888' }}
          >
            <h2>Set Quiz Topic</h2>
            <input
              type="text"
              value={topicInput}
              onChange={e => setTopicInput(e.target.value)}
              placeholder="Enter quiz topic"
              style={{ fontSize: 18, padding: 8, width: 240 }}
              autoFocus
            />
            <button type="submit" style={{ marginLeft: 12, fontSize: 18, padding: '8px 24px' }}>Set Topic</button>
          </form>
        </div>
      )}
      {/* Add spinner animation CSS */}
      <style>{`
@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
`}</style>
    </div>
  );
}

export default App;