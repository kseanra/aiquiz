import React, { useState, useRef, useEffect } from 'react';
import * as signalR from '@microsoft/signalr';
import './App.scss';
import { Console } from 'console';
import { stringify } from 'querystring';
import { IGameRoom } from './model';

const host = window.location.hostname; // dynamically resolves to localhost or IP
const port = 5000;
const HUB_URL = `http://${host}:${port}/quizhub`;

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
  const [numQuestions, setNumQuestions] = useState(4); // New state for number of questions
  const [countdown, setCountdown] = useState<number | null>(null); // Countdown seconds left
  const countdownInterval = useRef<ReturnType<typeof setInterval> | null>(null);
  const [showCreateRoom, setShowCreateRoom] = useState(false);
  const [privateRoomName, setPrivateRoomName] = useState('');
  const [privateRoomMaxPlayers, setPrivateRoomMaxPlayers] = useState(2);
  const [createdRoom, setCreatedRoomId] = useState<IGameRoom | null>(null);

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

  // Clean up countdown interval on unmount
  useEffect(() => {
    return () => {
      if (countdownInterval.current) {
        clearInterval(countdownInterval.current);
      }
    };
  }, []);

  const handleConnect = async () => {
    if (!name) return;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .configureLogging(signalR.LogLevel.Information)
      // .withAutomaticReconnect()
      .build();

    conn.on("ReceiveQuestion", (quiz: any) => {
      console.log(`on ReciveQuestion ${JSON.stringify(quiz)}`);
      setQuestion(quiz); // Set Quiz object in state
      setLoadingQuestion(false);
    });

    conn.on("PlayersStatus", (players: any[]) => {
      console.log("on PlayersStatus");
      setPlayerStates(players);
    });

    conn.on("Pong", (serverTime: string) => {
      setLastPong(serverTime);
    });

    conn.on("GameOver", (players: any[]) => {
      console.log("on GameOver");
      setGameOver(true);
      setQuestion(null);
      setSelectedOption('');
      setPlayerStates(players);
      setLoadingQuestion(false);
      // Find winner
      console.log("Players in GameOver:", JSON.stringify(players));
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

    conn.on("RoomCreated", (roomId: IGameRoom) => {
      setCreatedRoomId(roomId);
      setReady(true);
      setLoadingQuestion(true); // Show loading spinner when ready
    });
    conn.on("Error", (msg: string) => {
      alert(msg);
    });

    // --- COUNTDOWN LOGIC ---
    conn.on("StartCountdown", (totalSeconds: number) => {
      if (countdownInterval.current) {
        clearInterval(countdownInterval.current);
      }
      setCountdown(totalSeconds);
      countdownInterval.current = setInterval(() => {
        setCountdown(prev => {
          if (prev === null) return null;
          if (prev <= 1) {
            if (countdownInterval.current) {
              clearInterval(countdownInterval.current);
            }
            return null;
          }
          return prev - 1;
        });
      }, 1000);
    });

    // Optionally, handle server-side forced finish
    conn.on("CountdownFinished", () => {
      if (countdownInterval.current) {
        clearInterval(countdownInterval.current);
      }
      setCountdown(null);
    });

    try {
      await conn.start();
      await conn.invoke("SubmitName", name);
      setConnection(conn);
      setConnected(true);
      setReady(false);
    } catch (error) {
      console.error("Connection failed: ", error);
      alert(`Failed to connect. Please try again. ${error}`);
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
    <div className="app-container">
      {lastPong && (
        <div className="pong-bar">
          Last Pong from server: {lastPong}
        </div>
      )}
      <div style={{ marginTop: lastPong ? 40 : 0 }}>
        {/* Countdown timer display */}
        {!question && countdown !== null && countdown > 0 && (
          <div className="countdown-timer">
            <span>Game starting in: <strong>{countdown}</strong> second{countdown === 1 ? '' : 's'}...</span>
          </div>
        )}
        {!connected ? (
          <>
            <form
              className='form-margin-top'
              onSubmit={e => {
                e.preventDefault();
                handleConnect();
              }}
            >
              <label>
                Enter your name:
              </label>
               <input
                  type='text'
                  value={name}
                  onChange={e => setName(e.target.value)}
                  autoFocus
                />
              <button type="submit">Enter</button>
            </form>
          </>
        ) : (
          <div>
            <h2>Welcome, {name}! Connected to the game room.</h2>
            {/* Player Status List */}
            {playerStates.length > 0 && (
              <div className="player-status-list">
                <strong>Player Status:</strong>
                <ul>
                  {playerStates.map((p, idx) => (
                    <li key={p.connectionId || idx}>
                      {p.name || 'Anonymous'}: {statusToString(p.status)} (Question #{(p.currentQuestionIndex ?? 0) + 1})
                    </li>
                  ))}
                </ul>
              </div>
            )}
             {createdRoom  && (
              <div className="player-status-list">
                <strong>Private Room Created</strong>
                <div>Room ID: <strong>{createdRoom.roomName}</strong></div>
                <div>Password: <strong>{createdRoom.roomPassword}</strong></div>
                <button hidden= {true} onClick={() => { setCreatedRoomId(null); }}>OK</button>
              </div>
            )}
            {/* Show Ready and Create Private Game Room buttons side by side before user is ready */}
            {!ready && (
              <div style={{ display: 'flex', gap: 12, marginTop: 16 }}>
                <button className="ready-btn" onClick={handleReady}>
                  I am Ready
                </button>
                <button onClick={() => setShowCreateRoom(true)}>
                  Create Private Game Room
                </button>
              </div>
            )}
            {gameOver ? (
              <div className="game-over-box">
                <strong>Game Over!</strong><br />
                {winnerName ? `Winner: ${winnerName}` : 'No winner.'}
                <div style={{ marginTop: 16 }}>
                  <button onClick={handleExit}>Exit</button>
                </div>
              </div>
            ) : question && (
              <div className="question-box">
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
                  <ul className="quiz-options">
                    {question.options && question.options.map((opt: string, idx: number) => (
                      <li key={idx}>
                        <label>
                          <input
                            className="quiz-radio"
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
                  <div className="waiting-box">
                    <span className="spinner" />
                    <div>Waiting for next question...</div>
                  </div>
                )}
          </div>
        )}
      </div>
      {showBigCross && (
        <div className="big-cross">
          <span style={{ color: 'red', fontSize: 120, fontWeight: 'bold', textShadow: '2px 2px 8px #fff' }}>&#10060;</span>
        </div>
      )}
      {showSetTopic && (
        <div className="set-topic-modal">
          <form
            className="set-topic-form"
            onSubmit={async e => {
              e.preventDefault();
              if (connection && topicInput.trim() && numQuestions > 0) {
                 connection.invoke("SetQuizTopic", topicInput.trim(), numQuestions);
                setShowSetTopic(false);
                setTopicInput('');
                setNumQuestions(4);
              }
            }}
          >
            <h2>Set Quiz Topic</h2>
            <input
              type="text"
              value={topicInput}
              onChange={e => setTopicInput(e.target.value)}
              placeholder="Enter quiz topic"
              autoFocus
            />
            <input
              type="number"
              min={1}
              max={20}
              value={numQuestions}
              onChange={e => setNumQuestions(Number(e.target.value))}
              placeholder="Number of questions"
              style={{ marginLeft: 12, width: 80 }}
            />
            <button type="submit">Set Topic</button>
          </form>
        </div>
      )}
      {showCreateRoom && (
        <div className="set-topic-modal">
          <form
            className="set-topic-form"
            onSubmit={async e => {
              e.preventDefault();
              if (connection && privateRoomName.trim() && privateRoomMaxPlayers > 1) {
                await connection.invoke("CreatePrivateRoomAndReady", privateRoomName.trim(), topicInput.trim() || 'General', privateRoomMaxPlayers);
                setShowCreateRoom(false);
              }
            }}
          >
            <h2>Create Private Game Room</h2>
            <input
              type="text"
              value={privateRoomName}
              onChange={e => setPrivateRoomName(e.target.value)}
              placeholder="Room Name"
              autoFocus
            />
            <input
              type="number"
              min={2}
              max={20}
              value={privateRoomMaxPlayers}
              onChange={e => setPrivateRoomMaxPlayers(Number(e.target.value))}
              placeholder="Max Players"
              style={{ marginLeft: 12, width: 80 }}
            />
            <input
              type="text"
              value={topicInput}
              onChange={e => setTopicInput(e.target.value)}
              placeholder="Quiz Topic"
              style={{ marginLeft: 12, width: 120 }}
            />
            <button type="submit">Create Room</button>
            <button type="button" style={{marginLeft: 8}} onClick={() => setShowCreateRoom(false)}>Cancel</button>
          </form>
        </div>
      )}
    </div>
  );
}

export default App;