"use strict";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hub")
  .withAutomaticReconnect()
  .build();

let selectedAgent = null;
const agentStatuses = {};

connection.on("OnDelta", (agentName, text) => appendDelta(agentName, text));
connection.on("OnComplete", () => finalizeMessage());
connection.on("OnAgentStatusChanged", (agentName, status) => {
  agentStatuses[agentName] = status;
  updateAgentList();
});
connection.on("OnError", (agentName, message) => appendError(agentName, message));

connection.onreconnecting(() => {
  document.getElementById("connection-status").textContent = "Reconnecting...";
});
connection.onreconnected(async () => {
  document.getElementById("connection-status").textContent = "Connected";
  await refreshAgentStatus();
});
connection.onclose(() => {
  document.getElementById("connection-status").textContent = "Disconnected";
});

async function start() {
  await connection.start();
  document.getElementById("connection-status").textContent = "Connected";
  await refreshAgentStatus();
}

async function refreshAgentStatus() {
  try {
    const statuses = await connection.invoke("GetAgentStatus");
    Object.assign(agentStatuses, statuses);
    updateAgentList();
    if (!selectedAgent) {
      const first = Object.keys(agentStatuses)[0];
      if (first) selectAgent(first);
    }
  } catch (err) {
    console.error("Failed to refresh agent status:", err);
  }
}

function updateAgentList() {
  const list = document.getElementById("agent-list");
  list.innerHTML = "";
  for (const [name, status] of Object.entries(agentStatuses)) {
    const li = document.createElement("li");
    li.textContent = `${name} — ${status}`;
    li.dataset.agent = name;
    li.className = `status-${status.toLowerCase()}`;
    if (name === selectedAgent) li.classList.add("selected");
    li.addEventListener("click", () => selectAgent(name));
    list.appendChild(li);
  }
}

function selectAgent(name) {
  selectedAgent = name;
  document.querySelector("#agent-list .selected")?.classList.remove("selected");
  document.querySelector(`[data-agent="${name}"]`)?.classList.add("selected");
}

let currentMessageEl = null;

function appendDelta(agentName, text) {
  if (!currentMessageEl) {
    currentMessageEl = document.createElement("div");
    currentMessageEl.className = "message assistant";
    const label = document.createElement("strong");
    label.textContent = agentName + ": ";
    currentMessageEl.appendChild(label);
    document.getElementById("message-stream").appendChild(currentMessageEl);
  }
  currentMessageEl.appendChild(document.createTextNode(text));
  scrollToBottom();
}

function finalizeMessage() {
  currentMessageEl = null;
}

function appendError(agentName, message) {
  const div = document.createElement("div");
  div.className = "message error";
  div.textContent = `[${agentName} error] ${message}`;
  document.getElementById("message-stream").appendChild(div);
  scrollToBottom();
  currentMessageEl = null;
}

function scrollToBottom() {
  const stream = document.getElementById("message-stream");
  stream.scrollTop = stream.scrollHeight;
}

document.getElementById("send-btn").addEventListener("click", async () => {
  const input = document.getElementById("prompt-input");
  const text = input.value.trim();
  if (!text || !selectedAgent) return;

  const div = document.createElement("div");
  div.className = "message user";
  div.textContent = `You: ${text}`;
  document.getElementById("message-stream").appendChild(div);
  scrollToBottom();
  input.value = "";

  try {
    await connection.invoke("SendMessage", selectedAgent, text);
  } catch (err) {
    appendError(selectedAgent, err.toString());
  }
});

document.getElementById("prompt-input").addEventListener("keydown", (e) => {
  if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault();
    document.getElementById("send-btn").click();
  }
});

start().catch(console.error);
