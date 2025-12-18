window.empire = (function () {
    const state = {
        code: '',
        isTv: false,
        connection: null,
        voiceEnabled: false,
        lastPrompt: null
    };

    function initPage(options) {
        state.code = options.code;
        state.isTv = options.isTv;
        wireUpActions();
        startHub();
        loadState();
    }

    function wireUpActions() {
        if (state.isTv) {
            document.getElementById('nextPrompt')?.addEventListener('click', () => postJson('NextPrompt'));
            document.getElementById('startGame')?.addEventListener('click', () => postJson('Start'));
            document.getElementById('resetGame')?.addEventListener('click', () => postJson('Reset'));
            document.getElementById('toggleVoice')?.addEventListener('click', () => toggleVoice());
            const autoToggle = document.getElementById('autoAdvanceToggle');
            autoToggle?.addEventListener('change', () => postJson('ToggleAuto', { enabled: autoToggle.checked }));
        } else {
            document.getElementById('promptSubmit')?.addEventListener('click', () => {
                const value = document.getElementById('promptInput').value.trim();
                if (value.length === 0) return;
                postJson('SubmitPrompt', { prompt: value });
            });
        }
    }

    function toggleVoice() {
        state.voiceEnabled = !state.voiceEnabled;
        const btn = document.getElementById('toggleVoice');
        if (btn) {
            btn.textContent = state.voiceEnabled ? 'Disable voice' : 'Enable voice';
        }
    }

    async function startHub() {
        if (!window.signalR) return;
        state.connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/empire')
            .withAutomaticReconnect()
            .build();

        state.connection.on('GameUpdated', () => loadState());
        await state.connection.start();
        await state.connection.invoke('JoinGameGroup', state.code);
    }

    async function loadState() {
        const res = await fetch(`/game/${state.code}/state?tv=${state.isTv ? 1 : 0}`);
        if (!res.ok) return;
        const data = await res.json();
        if (state.isTv) {
            renderTv(data);
        } else {
            renderPhone(data);
        }
    }

    async function postJson(handler, payload) {
        const res = await fetch(`/game/${state.code}?handler=${handler}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: payload ? JSON.stringify(payload) : null
        });
        if (res.ok) {
            await loadState();
        }
    }

    function renderPhone(data) {
        const youId = data.yourPlayerId;
        const players = data.players || [];
        const you = players.find(p => p.id === youId);
        const promptStatus = document.getElementById('promptStatus');
        const promptInput = document.getElementById('promptInput');
        if (you) {
            promptStatus.textContent = you.promptSubmitted ? 'Submitted' : 'Pending';
            if (you.promptSubmitted) {
                promptInput.placeholder = 'Submitted';
            }
        }

        const roleStatus = document.getElementById('roleStatus');
        const guessActions = document.getElementById('guessActions');
        const targetList = document.getElementById('targetList');
        const pendingArea = document.getElementById('pendingArea');

        targetList.innerHTML = '';
        pendingArea.innerHTML = '';
        guessActions.classList.add('d-none');
        pendingArea.classList.add('d-none');

        const activeGuesser = players.find(p => p.id === data.activeGuesserId);
        if (data.phase === 'Finished') {
            roleStatus.textContent = `Game over. Winner: ${data.winnerName || ''}`;
            return;
        }

        if (data.pendingGuess && data.pendingGuess.targetId === youId) {
            pendingArea.classList.remove('d-none');
            pendingArea.innerHTML = `
                <div class="alert alert-warning">
                    <div class="fw-bold">${data.pendingGuess.guesserName} says they guessed you.</div>
                    <div class="mt-3 d-flex gap-2">
                        <button class="btn btn-success" id="confirmYes">Confirm correct</button>
                        <button class="btn btn-danger" id="confirmNo">Deny</button>
                    </div>
                </div>`;
            document.getElementById('confirmYes').onclick = () => postJson('Confirm', { confirm: true });
            document.getElementById('confirmNo').onclick = () => postJson('Confirm', { confirm: false });
        }

        if (youId && data.activeGuesserId === youId) {
            roleStatus.textContent = 'Your turn to guess';
            guessActions.classList.remove('d-none');
            const leaders = players.filter(p => !p.leaderId && p.id !== youId);
            if (leaders.length === 0) {
                targetList.innerHTML = '<div class="text-muted">No eligible targets.</div>';
            } else {
                leaders.forEach(target => {
                    const wrapper = document.createElement('div');
                    wrapper.className = 'd-flex flex-column border rounded p-2';
                    wrapper.innerHTML = `<div class="fw-semibold">${target.name}</div>`;
                    const correctBtn = document.createElement('button');
                    correctBtn.className = 'btn btn-success btn-sm mt-2';
                    correctBtn.textContent = 'Claim correct';
                    correctBtn.onclick = () => postJson('Claim', { targetId: target.id, outcome: 0 });
                    const wrongBtn = document.createElement('button');
                    wrongBtn.className = 'btn btn-outline-secondary btn-sm mt-1';
                    wrongBtn.textContent = 'Claim wrong';
                    wrongBtn.onclick = () => postJson('Claim', { targetId: target.id, outcome: 1 });
                    wrapper.appendChild(correctBtn);
                    wrapper.appendChild(wrongBtn);
                    targetList.appendChild(wrapper);
                });
            }
        } else {
            const guesserName = activeGuesser ? activeGuesser.name : 'Waiting to start';
            roleStatus.textContent = data.pendingGuess ? 'Resolving pending guess...' : `Waiting... Active guesser: ${guesserName}`;
        }
    }

    function renderTv(data) {
        const players = data.players || [];
        const guesser = players.find(p => p.id === data.activeGuesserId);
        const promptDisplay = document.getElementById('promptDisplay');
        const promptMeta = document.getElementById('promptMeta');
        const autoToggle = document.getElementById('autoAdvanceToggle');
        const pendingPanel = document.getElementById('pendingPanel');

        if (autoToggle) {
            autoToggle.checked = !!data.autoAdvancePrompts;
        }

        promptDisplay.textContent = data.currentPrompt || 'Waiting for prompts...';
        promptMeta.textContent = `Prompt ${data.promptIndex + 1} of ${Math.max(data.totalPrompts, 1)} • Cycle ${data.cycleCount + 1}`;

        const guesserEl = document.getElementById('tvGuesser');
        guesserEl.textContent = guesser ? guesser.name : '--';

        if (data.phase === 'Finished' && data.winnerName) {
            promptDisplay.textContent = `Winner: ${data.winnerName}`;
        }

        if (pendingPanel) {
            if (data.pendingGuess) {
                pendingPanel.innerHTML = `<div class="alert alert-info mb-0">${data.pendingGuess.guesserName} claims ${data.pendingGuess.targetName}. Waiting for confirmation.</div>`;
            } else {
                pendingPanel.textContent = 'None';
            }
        }

        renderEmpires(players, data.winnerId);
        speakPromptIfNeeded(data);
    }

    function renderEmpires(players, winnerId) {
        const container = document.getElementById('empireGrid');
        container.innerHTML = '';
        const leaders = players.filter(p => !p.leaderId);
        if (leaders.length === 0) {
            container.innerHTML = '<div class="text-muted">Waiting for players...</div>';
            return;
        }

        leaders.forEach(leader => {
            const recruits = players.filter(p => p.leaderId === leader.id);
            const card = document.createElement('div');
            card.className = 'card p-3 empire-card';
            const winnerBadge = leader.id === winnerId ? '<span class="badge bg-success ms-2">Winner</span>' : '';
            card.innerHTML = `<div class="fw-bold">${leader.name}${winnerBadge}</div>`;
            if (recruits.length) {
                const list = document.createElement('ul');
                list.className = 'mb-0 mt-2';
                recruits.forEach(r => {
                    const li = document.createElement('li');
                    li.textContent = r.name;
                    list.appendChild(li);
                });
                card.appendChild(list);
            } else {
                const empty = document.createElement('div');
                empty.className = 'text-muted small mt-2';
                empty.textContent = 'Independent';
                card.appendChild(empty);
            }
            container.appendChild(card);
        });
    }

    function speakPromptIfNeeded(data) {
        if (!state.isTv || !state.voiceEnabled || !window.speechSynthesis) return;
        if (!data.promptVisible || !data.currentPrompt || data.currentPrompt === state.lastPrompt) return;
        const utter = new SpeechSynthesisUtterance(data.currentPrompt);
        window.speechSynthesis.cancel();
        window.speechSynthesis.speak(utter);
        state.lastPrompt = data.currentPrompt;
    }

    return { initPage };
})();
