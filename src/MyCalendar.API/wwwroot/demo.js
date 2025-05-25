const API_BASE = window.location.origin + '/api';
let requestCounter = 0;
let currentUsers = [];
let currentMeetings = [];
let currentWeekStart = new Date();
let autoScroll = true;
let selectedUserId = null;
let editingUserId = null;
let selectedMeeting = null;

const MAJOR_TIMEZONES = [
    { value: 'UTC-12:00', label: 'UTC-12:00 (Baker Island)' },
    { value: 'UTC-11:00', label: 'UTC-11:00 (American Samoa)' },
    { value: 'UTC-10:00', label: 'UTC-10:00 (Hawaii)' },
    { value: 'UTC-9:30', label: 'UTC-9:30 (Marquesas Islands)' },
    { value: 'UTC-9:00', label: 'UTC-9:00 (Alaska)' },
    { value: 'UTC-8:00', label: 'UTC-8:00 (Pacific Time - Los Angeles)' },
    { value: 'UTC-7:00', label: 'UTC-7:00 (Mountain Time - Denver)' },
    { value: 'UTC-6:00', label: 'UTC-6:00 (Central Time - Chicago)' },
    { value: 'UTC-5:00', label: 'UTC-5:00 (Eastern Time - New York)' },
    { value: 'UTC-4:00', label: 'UTC-4:00 (Atlantic Time - Halifax)' },
    { value: 'UTC-3:30', label: 'UTC-3:30 (Newfoundland)' },
    { value: 'UTC-3:00', label: 'UTC-3:00 (Brazil - São Paulo)' },
    { value: 'UTC-2:00', label: 'UTC-2:00 (South Georgia)' },
    { value: 'UTC-1:00', label: 'UTC-1:00 (Azores)' },
    { value: 'UTC', label: 'UTC (Greenwich Mean Time - London)' },
    { value: 'UTC+1:00', label: 'UTC+1:00 (Central Europe - Berlin, Paris)' },
    { value: 'UTC+2:00', label: 'UTC+2:00 (Eastern Europe - Cairo, Athens)' },
    { value: 'UTC+3:00', label: 'UTC+3:00 (Moscow, Istanbul)' },
    { value: 'UTC+3:30', label: 'UTC+3:30 (Iran - Tehran)' },
    { value: 'UTC+4:00', label: 'UTC+4:00 (Gulf - Dubai, Abu Dhabi)' },
    { value: 'UTC+4:30', label: 'UTC+4:30 (Afghanistan - Kabul)' },
    { value: 'UTC+5:00', label: 'UTC+5:00 (Pakistan - Karachi)' },
    { value: 'UTC+5:30', label: 'UTC+5:30 (India - Mumbai, Delhi)' },
    { value: 'UTC+5:45', label: 'UTC+5:45 (Nepal - Kathmandu)' },
    { value: 'UTC+6:00', label: 'UTC+6:00 (Bangladesh - Dhaka)' },
    { value: 'UTC+6:30', label: 'UTC+6:30 (Myanmar - Yangon)' },
    { value: 'UTC+7:00', label: 'UTC+7:00 (Thailand - Bangkok)' },
    { value: 'UTC+8:00', label: 'UTC+8:00 (China - Beijing, Singapore)' },
    { value: 'UTC+8:30', label: 'UTC+8:30 (North Korea - Pyongyang)' },
    { value: 'UTC+9:00', label: 'UTC+9:00 (Japan - Tokyo, Korea - Seoul)' },
    { value: 'UTC+9:30', label: 'UTC+9:30 (Australia - Adelaide)' },
    { value: 'UTC+10:00', label: 'UTC+10:00 (Australia - Sydney, Melbourne)' },
    { value: 'UTC+10:30', label: 'UTC+10:30 (Australia - Lord Howe Island)' },
    { value: 'UTC+11:00', label: 'UTC+11:00 (Solomon Islands)' },
    { value: 'UTC+12:00', label: 'UTC+12:00 (New Zealand - Auckland)' },
    { value: 'UTC+12:45', label: 'UTC+12:45 (New Zealand - Chatham Islands)' },
    { value: 'UTC+13:00', label: 'UTC+13:00 (Tonga)' },
    { value: 'UTC+14:00', label: 'UTC+14:00 (Kiribati - Line Islands)' }
];

function generateTimezoneOptions(selectedValue = 'UTC') {
    return MAJOR_TIMEZONES.map(tz => 
        `<option value="${tz.value}" ${tz.value === selectedValue ? 'selected' : ''}>${tz.label}</option>`
    ).join('');
}

document.addEventListener('DOMContentLoaded', function() {
    initializeDates();
    setCurrentWeek();
    loadDashboard();
    loadCalendar();
});

function initializeDates() {
    const now = new Date();
    const tomorrow = new Date(now);
    tomorrow.setDate(tomorrow.getDate() + 1);
    tomorrow.setHours(14, 0, 0, 0);
    
    const endTime = new Date(tomorrow);
    endTime.setHours(15, 0, 0, 0);
    
    document.getElementById('startTime').value = tomorrow.toISOString().slice(0, 16);
    document.getElementById('endTime').value = endTime.toISOString().slice(0, 16);
}

function setCurrentWeek() {
    const today = new Date();
    const dayOfWeek = today.getDay();
    const diff = today.getDate() - dayOfWeek + (dayOfWeek === 0 ? -6 : 1);
    currentWeekStart = new Date(today.setDate(diff));
    currentWeekStart.setHours(0, 0, 0, 0);
}

function getMeetingColor(meetingId) {

    const hash = meetingId.split('').reduce((a, b) => {
        a = ((a << 5) - a) + b.charCodeAt(0);
        return a & a;
    }, 0);
    
    const colorIndex = Math.abs(hash) % 10 + 1;
    return `color-${colorIndex}`;
}

async function makeRequest(method, url, data = null) {
    requestCounter++;
    const timestamp = new Date().toLocaleTimeString();
    
    logRequest(`[${requestCounter}] ${timestamp} - ${method} ${url}`, data);
    
    try {
        const options = {
            method: method,
            headers: {
                'Content-Type': 'application/json',
            }
        };
        
        if (data) {
            options.body = JSON.stringify(data);
        }
        
        const response = await fetch(url, options);
        const responseData = await response.json().catch(() => null);
        
        logResponse(response.status, responseData);
        return { status: response.status, data: responseData };
        
    } catch (error) {
        logError('Erro de conexão: ' + error.message);
        return { status: 0, error: error.message };
    }
}

function logRequest(message, data) {
    const logs = document.getElementById('requestLogs');
    logs.innerHTML += `\n🚀 ${message}`;
    if (data) {
        logs.innerHTML += `\n📤 Body: ${JSON.stringify(data, null, 2)}`;
    }
    if (autoScroll) {
    logs.scrollTop = logs.scrollHeight;
    }
}

function logResponse(status, data) {
    const logs = document.getElementById('requestLogs');
    const statusIcon = status >= 200 && status < 300 ? '✅' : status === 409 ? '⚠️' : '❌';
    logs.innerHTML += `\n${statusIcon} Response [${status}]: ${JSON.stringify(data, null, 2)}\n${'='.repeat(50)}`;
    if (autoScroll) {
    logs.scrollTop = logs.scrollHeight;
    }
}

function logError(message) {
    const logs = document.getElementById('requestLogs');
    logs.innerHTML += `\n❌ ${message}\n${'='.repeat(50)}`;
    if (autoScroll) {
    logs.scrollTop = logs.scrollHeight;
    }
}

function showResponse(elementId, status, data, isSuccess = true) {
    const element = document.getElementById(elementId);
    element.style.display = 'block';
    element.className = `response-box ${isSuccess ? 'success' : status === 409 ? 'warning' : 'error'}`;
    element.textContent = JSON.stringify(data, null, 2);
}

async function loadDashboard() {
    await loadUsers();
    await loadMeetings();
    updateDashboardStats();
}

function updateDashboardStats() {
    const stats = {
        totalUsers: currentUsers.length,
        totalMeetings: currentMeetings.length,
        conflictedMeetings: currentMeetings.filter(m => m.hasConflicts).length,
        upcomingMeetings: currentMeetings.filter(m => new Date(m.startTime) > new Date()).length
    };
    
    const statsHtml = `
        <div class="dashboard-stats">
            <div class="stat-card">
                <div class="stat-number">${stats.totalUsers}</div>
                <div class="stat-label">👤 Total de Usuários</div>
            </div>
            <div class="stat-card">
                <div class="stat-number">${stats.totalMeetings}</div>
                <div class="stat-label">📅 Total de Reuniões</div>
            </div>
            <div class="stat-card">
                <div class="stat-number">${stats.conflictedMeetings}</div>
                <div class="stat-label">⚠️ Conflitos</div>
            </div>
            <div class="stat-card">
                <div class="stat-number">${stats.upcomingMeetings}</div>
                <div class="stat-label">🔮 Próximas Reuniões</div>
            </div>
        </div>
    `;
    
    const element = document.getElementById('dashboardStats');
    element.innerHTML = statsHtml;
    element.style.display = 'block';
}

async function createUser() {
    const name = document.getElementById('userName').value;
    const timeZone = document.getElementById('userTimezone').value;
    
    if (!name) {
        alert('Por favor, insira um nome');
        return;
    }
    
    const result = await makeRequest('POST', `${API_BASE}/Users`, {
        name: name,
        timeZone: timeZone
    });
    
            if (result.status === 201) {
            document.getElementById('userName').value = '';
            await loadUsers();
            updateParticipantsSelector();
            updateDashboardStats();
            
            const responseElement = document.getElementById('usersResponse');
            if (responseElement) {
                responseElement.style.display = 'none';
            }
        } else {
            showResponse('usersResponse', result.status, result.data, false);
        }
}

async function loadUsers() {
    const result = await makeRequest('GET', `${API_BASE}/Users`);
    if (result.status === 200) {
        currentUsers = result.data || [];
        displayUsersList();
        updateParticipantsSelector();
    }
    
    const responseElement = document.getElementById('usersResponse');
    if (responseElement) {
        responseElement.style.display = 'none';
    }

    if (result.status !== 200) {
        showResponse('usersResponse', result.status, result.data, false);
    }
}

function displayUsersList() {
    const usersListElement = document.getElementById('usersList');
    const usersTableElement = document.getElementById('usersTable');
    
    if (currentUsers.length === 0) {
        usersListElement.style.display = 'none';
        return;
    }
    
    let tableHtml = `
        <h4>
            👥 Lista de Usuários
            <button class="btn btn-secondary json-toggle-btn" onclick="toggleUsersJson()" id="jsonToggleBtn">📄 VER JSON</button>
        </h4>
        
        <div id="usersJsonView" style="display: none;">
            <pre style="background: #f8f9fa; padding: 15px; border-radius: 8px; border: 1px solid #e1e8ed; max-height: 300px; overflow-y: auto; font-size: 12px;">${JSON.stringify(currentUsers, null, 2)}</pre>
        </div>
        
        <div id="usersTableView">
            <table class="users-table">
                <thead>
                    <tr>
                        <th>Nome</th>
                        <th>Timezone</th>
                        <th>ID</th>
                        <th>Ações</th>
                    </tr>
                </thead>
                <tbody>
    `;
    
    currentUsers.forEach(user => {
        const isEditing = editingUserId === user.id;
        const rowClass = isEditing ? 'editing' : (selectedUserId === user.id ? 'selected' : '');
        
        tableHtml += `
            <tr class="${rowClass}" id="user-row-${user.id}">
                <td class="editable-cell">
                    ${isEditing ? 
                        `<input type="text" class="editable-input" id="edit-name-${user.id}" value="${user.name}">` :
                        user.name
                    }
                </td>
                <td class="editable-cell">
                    ${isEditing ? 
                        `<select class="editable-select" id="edit-timezone-${user.id}">
                            ${generateTimezoneOptions(user.timeZone)}
                        </select>` :
                        user.timeZone
                    }
                </td>
                <td class="user-id-cell" title="${user.id}">${user.id}</td>
                <td>
                    ${isEditing ? 
                        `<div class="edit-actions">
                            <button class="btn btn-save" onclick="saveUser('${user.id}')">💾</button>
                            <button class="btn btn-cancel" onclick="cancelEdit()">❌</button>
                        </div>` :
                        `<div class="user-actions">
                            <button class="btn btn-secondary" onclick="editUser('${user.id}')" title="Editar">✏️</button>
                            <button class="btn btn-secondary" onclick="deleteUser('${user.id}')" title="Excluir">🗑️</button>
                        </div>`
                    }
                </td>
            </tr>
        `;
    });
    
    tableHtml += `
                </tbody>
            </table>
        </div>
    `;
    
    usersTableElement.innerHTML = tableHtml;
    usersListElement.style.display = 'block';
}

function toggleUsersJson() {
    const jsonView = document.getElementById('usersJsonView');
    const tableView = document.getElementById('usersTableView');
    const toggleBtn = document.getElementById('jsonToggleBtn');
    
    if (jsonView.style.display === 'none') {
        jsonView.style.display = 'block';
        tableView.style.display = 'none';
        toggleBtn.textContent = '📋 VER TABELA';
        
        const jsonPre = jsonView.querySelector('pre');
        if (jsonPre) {
            jsonPre.textContent = JSON.stringify(currentUsers, null, 2);
        }
    } else {
        jsonView.style.display = 'none';
        tableView.style.display = 'block';
        toggleBtn.textContent = '📄 VER JSON';
    }
}

function editUser(userId) {
    if (editingUserId) {
        cancelEdit();
    }
    
    editingUserId = userId;
    displayUsersList();
    
    const nameInput = document.getElementById(`edit-name-${userId}`);
    if (nameInput) {
        nameInput.focus();
        nameInput.select();
    }
}

async function saveUser(userId) {
    const nameInput = document.getElementById(`edit-name-${userId}`);
    const timezoneSelect = document.getElementById(`edit-timezone-${userId}`);
    
    if (!nameInput || !timezoneSelect) {
        alert('Erro ao obter dados do formulário');
        return;
    }
    
    const newName = nameInput.value.trim();
    const newTimezone = timezoneSelect.value;
    
    if (!newName) {
        alert('Nome não pode estar vazio');
        nameInput.focus();
        return;
    }
    
    const row = document.getElementById(`user-row-${userId}`);
    if (row) {
        row.style.opacity = '0.6';
    }
    
    try {
        const result = await makeRequest('PUT', `${API_BASE}/Users/${userId}`, {
            name: newName,
            timeZone: newTimezone
        });
        
        if (result.status === 204) {
            const userIndex = currentUsers.findIndex(u => u.id === userId);
            if (userIndex !== -1) {
                currentUsers[userIndex].name = newName;
                currentUsers[userIndex].timeZone = newTimezone;
            }
            
            editingUserId = null;
            displayUsersList();
            updateParticipantsSelector();
            
            const responseElement = document.getElementById('usersResponse');
            if (responseElement) {
                responseElement.style.display = 'none';
            }
            
        } else {
            throw new Error(`Erro ${result.status}: ${result.data?.message || 'Falha ao atualizar usuário'}`);
        }
        
    } catch (error) {
        alert('Erro ao salvar: ' + error.message);
        
        if (row) {
            row.style.opacity = '1';
        }
        
        showResponse('usersResponse', 0, { error: error.message }, false);
    }
}

function cancelEdit() {
    editingUserId = null;
    displayUsersList();
}

function selectUser(id, name, timeZone) {
    if (editingUserId) {
        return;
    }
    
    selectedUserId = selectedUserId === id ? null : id;
    document.getElementById('userName').value = selectedUserId ? name : '';
    document.getElementById('userTimezone').value = selectedUserId ? timeZone : 'UTC';
    displayUsersList();
}

async function updateSelectedUser() {
    if (!selectedUserId) {
        alert('Selecione um usuário primeiro clicando na linha da tabela');
        return;
    }
    
    editUser(selectedUserId);
}

async function deleteSelectedUser() {
    if (!selectedUserId) {
        alert('Selecione um usuário primeiro clicando na linha da tabela');
        return;
    }
    
    await deleteUser(selectedUserId);
}

async function deleteUser(userId) {
    const user = currentUsers.find(u => u.id === userId);
    const userName = user ? user.name : 'usuário';
    
    if (!confirm(`Tem certeza que deseja excluir o usuário "${userName}"?`)) {
        return;
    }
    
    const row = document.getElementById(`user-row-${userId}`);
    if (row) {
        row.style.opacity = '0.6';
    }
    
    try {
        const result = await makeRequest('DELETE', `${API_BASE}/Users/${userId}`);
        
        if (result.status === 204) {
            currentUsers = currentUsers.filter(u => u.id !== userId);
            
            if (selectedUserId === userId) {
                selectedUserId = null;
                document.getElementById('userName').value = '';
                document.getElementById('userTimezone').value = 'UTC';
            }
            
            if (editingUserId === userId) {
                editingUserId = null;
            }
            
            displayUsersList();
            updateParticipantsSelector();
            updateDashboardStats();
            
            const responseElement = document.getElementById('usersResponse');
            if (responseElement) {
                responseElement.style.display = 'none';
            }
            
        } else {
            throw new Error(`Erro ${result.status}: ${result.data?.message || 'Falha ao excluir usuário'}`);
        }
        
    } catch (error) {
        alert('Erro ao excluir: ' + error.message);
        
        if (row) {
            row.style.opacity = '1';
        }
        
        showResponse('usersResponse', 0, { error: error.message }, false);
    }
}

async function createSampleUsers() {
    const sampleUsers = [
        { name: 'João Silva', timeZone: 'UTC-3:00' },
        { name: 'Maria Santos', timeZone: 'UTC-5:00' },
        { name: 'Pedro Costa', timeZone: 'UTC' },
        { name: 'Ana Oliveira', timeZone: 'UTC+1:00' },
        { name: 'Carlos Ferreira', timeZone: 'UTC+9:00' },
        { name: 'Priya Sharma', timeZone: 'UTC+5:30' },
        { name: 'Li Wei', timeZone: 'UTC+8:00' },
        { name: 'Ahmed Hassan', timeZone: 'UTC+3:00' },
        { name: 'Sarah Johnson', timeZone: 'UTC-8:00' },
        { name: 'Hiroshi Tanaka', timeZone: 'UTC+9:00' }
    ];
    
    for (const user of sampleUsers) {
        await makeRequest('POST', `${API_BASE}/Users`, user);
    }
    
    await loadUsers();
    updateParticipantsSelector();
    updateDashboardStats();
}

function updateParticipantsSelector() {
    const selector = document.getElementById('participantsSelector');
    
    if (currentUsers.length === 0) {
        selector.innerHTML = '<p>Carregue os usuários primeiro para selecionar participantes</p>';
        return;
    }
    
    let html = '';
    currentUsers.forEach(user => {
        html += `
            <div class="participant-item">
                <input type="checkbox" id="participant_${user.id}" value="${user.id}">
                <label for="participant_${user.id}">${user.name}</label>
                <span class="participant-timezone">${user.timeZone}</span>
            </div>
        `;
    });
    
    selector.innerHTML = html;
}

function getSelectedParticipants() {
    const checkboxes = document.querySelectorAll('#participantsSelector input[type="checkbox"]:checked');
    return Array.from(checkboxes).map(cb => cb.value);
}

async function scheduleMeeting() {
    const title = document.getElementById('meetingTitle').value;
    const startTime = document.getElementById('startTime').value;
    const endTime = document.getElementById('endTime').value;
    const participantIds = getSelectedParticipants();
    
    if (!title || !startTime || !endTime || participantIds.length === 0) {
        alert('Por favor, preencha todos os campos e selecione pelo menos um participante');
        return;
    }
    
    const result = await makeRequest('POST', `${API_BASE}/Meetings/schedule`, {
        title: title,
        startTime: startTime + ':00.000Z',
        endTime: endTime + ':00.000Z',
        participantIds: participantIds
    });
    
    showResponse('meetingResponse', result.status, result.data, result.status === 200);
    
    if (result.status === 200 || result.status === 409) {
        await loadMeetings();
        updateDashboardStats();
        await loadCalendar();
        
        if (result.status === 200) {
            document.getElementById('meetingTitle').value = '';
        }
    }
}

async function findAvailableSlots() {
    const participantIds = getSelectedParticipants();
    
    if (participantIds.length === 0) {
        alert('Por favor, selecione pelo menos um participante');
        return;
    }
    
    const startDate = new Date().toISOString();
    const endDate = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();
    
    const params = new URLSearchParams({
        startDate: startDate,
        endDate: endDate,
        durationMinutes: '60'
    });
    
    participantIds.forEach(id => params.append('participantIds', id));
    
    const result = await makeRequest('GET', `${API_BASE}/Meetings/available-slots?${params}`);
    showResponse('meetingResponse', result.status, result.data, result.status === 200);
}

async function loadMeetings() {
    try {
        const result = await makeRequest('GET', `${API_BASE}/Meetings`);
        if (result.status === 200) {
            currentMeetings = result.data || [];
            return currentMeetings;
        } else {
            console.error('Erro ao carregar reuniões:', result);
            currentMeetings = [];
            return [];
        }
    } catch (error) {
        console.error('Erro ao carregar reuniões:', error);
        currentMeetings = [];
        return [];
    }
}

function getMeetingsForSlot(cellDate) {
    const cellStart = new Date(cellDate);
    const cellEnd = new Date(cellDate);
    cellEnd.setUTCHours(cellEnd.getUTCHours() + 1);
    
    return currentMeetings.filter(meeting => {
        const meetingStart = new Date(meeting.startTime);
        const meetingEnd = new Date(meeting.endTime);
        
        return meetingStart < cellEnd && meetingEnd > cellStart;
    });
}

function renderCalendarGrid() {
    const grid = document.getElementById('calendarGrid');
    const days = ['Hora', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado', 'Domingo'];
    
    let html = '';
    
    days.forEach(day => {
        html += `<div class="calendar-day-header">${day}</div>`;
    });
    
    for (let hour = 8; hour <= 18; hour++) {
        html += `<div class="calendar-time-slot">${hour}:00 UTC</div>`;
        
        for (let day = 0; day < 7; day++) {
            const cellDate = new Date(currentWeekStart);
            cellDate.setUTCDate(cellDate.getUTCDate() + day);
            cellDate.setUTCHours(hour, 0, 0, 0);
            
            const meetings = getMeetingsForSlot(cellDate);
            const hasMeeting = meetings.length > 0;
            
            html += `<div class="calendar-cell ${hasMeeting ? 'has-meeting' : ''}" data-date="${cellDate.toISOString()}">`;
            
            if (hasMeeting) {
                const meeting = meetings[0];
                const hasConflict = checkMeetingConflict(meeting, meetings);
                const colorClass = hasConflict ? 'conflict' : getMeetingColor(meeting.id);
                const participantNames = meeting.participants?.map(p => p.name).join(', ') || 'Sem participantes';
                
                const meetingStartUtc = new Date(meeting.startTime);
                const meetingEndUtc = new Date(meeting.endTime);
                const startTimeUtc = `${meetingStartUtc.getUTCHours().toString().padStart(2, '0')}:${meetingStartUtc.getUTCMinutes().toString().padStart(2, '0')}`;
                const endTimeUtc = `${meetingEndUtc.getUTCHours().toString().padStart(2, '0')}:${meetingEndUtc.getUTCMinutes().toString().padStart(2, '0')}`;
                
                html += `<div class="calendar-meeting ${colorClass}" 
                            onclick="showMeetingDetails('${meeting.id}')"
                            title="${meeting.title}\n${startTimeUtc} - ${endTimeUtc} UTC\nParticipantes: ${participantNames}">
                            ${meeting.title}
                         </div>`;
                
                if (meetings.length > 1) {
                    html += `<div style="position: absolute; top: 2px; right: 2px; background: rgba(255,255,255,0.8); color: #333; border-radius: 50%; width: 16px; height: 16px; font-size: 10px; display: flex; align-items: center; justify-content: center; font-weight: bold;">+${meetings.length - 1}</div>`;
                }
            }
            
            html += '</div>';
        }
    }
    
    grid.innerHTML = html;
}

function checkMeetingConflict(meeting, allMeetingsInSlot) {
    const meetingParticipantIds = meeting.participants?.map(p => p.id) || [];
    
    return allMeetingsInSlot.some(otherMeeting => {
        if (otherMeeting.id === meeting.id) return false;
        
        const otherParticipantIds = otherMeeting.participants?.map(p => p.id) || [];
        return meetingParticipantIds.some(id => otherParticipantIds.includes(id));
    });
}

async function loadCalendar() {
    await loadMeetings();
    renderCalendarHeader();
    renderCalendarGrid();
}

function renderCalendarHeader() {
    const header = document.getElementById('calendarHeader');
    const weekStart = new Date(currentWeekStart);
    const weekEnd = new Date(weekStart);
    weekEnd.setDate(weekEnd.getDate() + 6);
    
    const options = { year: 'numeric', month: 'long', day: 'numeric' };
    const startStr = weekStart.toLocaleDateString('pt-BR', options);
    const endStr = weekEnd.toLocaleDateString('pt-BR', options);
    
    header.innerHTML = `
        <div class="calendar-title">
            📅 Semana de ${startStr} - ${endStr}
        </div>
    `;
}

function previousWeek() {
    currentWeekStart.setDate(currentWeekStart.getDate() - 7);
    loadCalendar();
}

function nextWeek() {
    currentWeekStart.setDate(currentWeekStart.getDate() + 7);
    loadCalendar();
}

function goToToday() {
    setCurrentWeek();
    loadCalendar();
}



async function createSampleData() {
    await createSampleUsers();
    if (currentUsers.length >= 2) {
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        tomorrow.setHours(14, 0, 0, 0);
        
        const endTime = new Date(tomorrow);
        endTime.setHours(15, 0, 0, 0);
        
        await makeRequest('POST', `${API_BASE}/Meetings/schedule`, {
            title: 'Reunião de Exemplo',
            startTime: tomorrow.toISOString(),
            endTime: endTime.toISOString(),
            participantIds: [currentUsers[0].id, currentUsers[1].id]
        });
    }
    
    updateDashboardStats();
    loadCalendar();
}

async function clearAllData() {
    if (!confirm('Tem certeza que deseja excluir todos os dados?')) {
        return;
    }
    
    for (const user of currentUsers) {
        await makeRequest('DELETE', `${API_BASE}/Users/${user.id}`);
    }
    
    await loadUsers();
    updateDashboardStats();
    loadCalendar();
}

function clearLogs() {
    document.getElementById('requestLogs').innerHTML = 'Logs de requisições aparecerão aqui...';
    requestCounter = 0;
}   

function exportLogs() {
    const logs = document.getElementById('requestLogs').textContent;
    const blob = new Blob([logs], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `mycalendar-logs-${new Date().toISOString().slice(0, 19)}.txt`;
    a.click();
    URL.revokeObjectURL(url);
}

function toggleAutoScroll() {
    autoScroll = !autoScroll;
    const button = event.target;
    button.textContent = autoScroll ? '📜 Auto-scroll' : '📜 Manual-scroll';
}

document.addEventListener('keydown', function(event) {
    if (editingUserId) {
        if (event.key === 'Enter') {
            event.preventDefault();
            saveUser(editingUserId);
        } else if (event.key === 'Escape') {
            event.preventDefault();
            cancelEdit();
        }
    }
});   

async function analyzeConflicts() {
    const participantIds = getSelectedParticipants();
    
    if (participantIds.length === 0) {
        alert('Por favor, selecione pelo menos um participante para análise');
        return;
    }
    
    const startTime = document.getElementById('startTime').value;
    const endTime = document.getElementById('endTime').value;
    
    let startDate, endDate, meetingStartTime, meetingEndTime;
    
    if (startTime && endTime) {
        meetingStartTime = startTime + ':00.000Z';
        meetingEndTime = endTime + ':00.000Z';
        
        const selectedDate = new Date(startTime + ':00.000Z');
        startDate = selectedDate.toISOString();
        
        const searchEndDate = new Date(selectedDate);
        searchEndDate.setDate(searchEndDate.getDate() + 7);
        endDate = searchEndDate.toISOString();
    } else {
        startDate = new Date().toISOString();
        endDate = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();
    }
    
    const result = await makeRequest('POST', `${API_BASE}/Meetings/analyze-conflicts`, {
        participantIds: participantIds,
        startDate: startDate,
        endDate: endDate,
        meetingStartTime: meetingStartTime,
        meetingEndTime: meetingEndTime,
        durationMinutes: 60
    });
    
    if (result.status === 200) {
        displayConflictAnalysis(result.data);
    } else {
        showResponse('meetingResponse', result.status, result.data, false);
    }
}

function displayConflictAnalysis(analysis) {
    const responseElement = document.getElementById('meetingResponse');
    responseElement.style.display = 'block';
    responseElement.className = 'response-box success';
    
    let html = `
        <div style="font-family: monospace; white-space: pre-line; line-height: 1.6;">
            <h4 style="color: #333; margin-bottom: 15px;">🔍 Análise de Conflitos e Horários</h4>
            
            <div style="background: #f8f9fa; padding: 15px; border-radius: 8px; margin-bottom: 15px;">
                <strong>📋 Resumo:</strong>
                ${analysis.summary}
            </div>
    `;
    
    if (analysis.participants && analysis.participants.length > 0) {
        html += `
            <div style="background: #e3f2fd; padding: 15px; border-radius: 8px; margin-bottom: 15px;">
                <strong>👥 Participantes:</strong>
        `;
        
        analysis.participants.forEach(participant => {
            html += `
                <div style="margin: 8px 0; padding: 8px; background: white; border-radius: 4px;">
                    <strong>${participant.name}</strong> (${participant.timeZone})
                    <br>• Horário local: ${participant.localWorkingHours}
                    <br>• Horário UTC: ${participant.utcWorkingHours}
                    <br>• Reuniões no período: ${participant.totalMeetings}
                </div>
            `;
        });
        
        html += `</div>`;
    }
    
    if (analysis.workingHoursOverlap) {
        const overlap = analysis.workingHoursOverlap;
        const bgColor = overlap.hasOverlap ? '#d4edda' : '#f8d7da';
        const icon = overlap.hasOverlap ? '✅' : '❌';
        
        html += `
            <div style="background: ${bgColor}; padding: 15px; border-radius: 8px; margin-bottom: 15px;">
                <strong>${icon} Sobreposição de Horários Comerciais:</strong>
                <div style="margin: 8px 0;">
                    <strong>Período:</strong> ${overlap.overlapPeriod}
                    <br><strong>Duração:</strong> ${overlap.overlapDuration}
                </div>
        `;
        
        if (overlap.participantLocalTimes && overlap.participantLocalTimes.length > 0) {
            html += `<div style="margin-top: 10px;"><strong>Horários locais:</strong></div>`;
            overlap.participantLocalTimes.forEach(time => {
                html += `<div style="margin: 4px 0; padding: 4px; background: rgba(255,255,255,0.7); border-radius: 3px;">• ${time}</div>`;
            });
        }
        
        html += `</div>`;
    }
    
    if (analysis.conflictingMeetings && analysis.conflictingMeetings.length > 0) {
        html += `
            <div style="background: #fff3cd; padding: 15px; border-radius: 8px; margin-bottom: 15px;">
                <strong>⚠️ Reuniões em Conflito (${analysis.conflictingMeetings.length}):</strong>
        `;
        
        analysis.conflictingMeetings.forEach(meeting => {
            const startTime = new Date(meeting.startTime).toLocaleString('pt-BR');
            const endTime = new Date(meeting.endTime).toLocaleString('pt-BR');
            
            html += `
                <div style="margin: 8px 0; padding: 8px; background: white; border-radius: 4px; border-left: 4px solid #ffc107;">
                    <strong>${meeting.title}</strong>
                    <br>• Horário: ${startTime} - ${endTime}
                    <br>• Participantes em conflito: ${meeting.conflictingParticipants.join(', ')}
                </div>
            `;
        });
        
        html += `</div>`;
    }
    
    if (analysis.suggestedTimeSlots && analysis.suggestedTimeSlots.length > 0) {
        html += `
            <div style="background: #d1ecf1; padding: 15px; border-radius: 8px; margin-bottom: 15px;">
                <strong>🎯 Horários Sugeridos (${analysis.suggestedTimeSlots.length}):</strong>
        `;
        
        analysis.suggestedTimeSlots.forEach((slot, index) => {
            html += `
                <div style="margin: 10px 0; padding: 12px; background: white; border-radius: 4px; border-left: 4px solid #17a2b8;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                        <strong>Opção ${index + 1}: ${slot.utcTimeRange}</strong>
                        <span style="background: #17a2b8; color: white; padding: 2px 8px; border-radius: 12px; font-size: 12px;">${slot.recommendation}</span>
                    </div>
            `;
            
            if (slot.participantLocalTimes && slot.participantLocalTimes.length > 0) {
                html += `<div style="margin-top: 8px;"><strong>Horários locais:</strong></div>`;
                slot.participantLocalTimes.forEach(time => {
                    html += `<div style="margin: 2px 0;">• ${time.name} (${time.timeZone}): ${time.localTimeRange}</div>`;
                });
            }
            
            html += `</div>`;
        });
        
        html += `</div>`;
    }
    
    html += `</div>`;
    responseElement.innerHTML = html;
}

function showMeetingDetails(meetingId) {
    const meeting = currentMeetings.find(m => m.id === meetingId);
    if (!meeting) {
        alert('Reunião não encontrada');
        return;
    }
    
    selectedMeeting = meeting;
    
    const modalBody = document.getElementById('meetingModalBody');
    const startTimeUtc = new Date(meeting.startTime);
    const endTimeUtc = new Date(meeting.endTime);
    
    const startTimeStr = `${startTimeUtc.getUTCDate().toString().padStart(2, '0')}/${(startTimeUtc.getUTCMonth() + 1).toString().padStart(2, '0')}/${startTimeUtc.getUTCFullYear()} ${startTimeUtc.getUTCHours().toString().padStart(2, '0')}:${startTimeUtc.getUTCMinutes().toString().padStart(2, '0')} UTC`;
    const endTimeStr = `${endTimeUtc.getUTCDate().toString().padStart(2, '0')}/${(endTimeUtc.getUTCMonth() + 1).toString().padStart(2, '0')}/${endTimeUtc.getUTCFullYear()} ${endTimeUtc.getUTCHours().toString().padStart(2, '0')}:${endTimeUtc.getUTCMinutes().toString().padStart(2, '0')} UTC`;
    
    let participantsHtml = '';
    if (meeting.participants && meeting.participants.length > 0) {
        participantsHtml = `
            <ul class="meeting-participants-list">
                ${meeting.participants.map(participant => `
                    <li class="meeting-participant-item">
                        <span class="meeting-participant-name">${participant.name}</span>
                        <span class="meeting-participant-timezone">${participant.timeZone}</span>
                    </li>
                `).join('')}
            </ul>
        `;
    } else {
        participantsHtml = '<p class="meeting-detail-value">Nenhum participante</p>';
    }
    
    modalBody.innerHTML = `
        <div class="meeting-detail-item">
            <span class="meeting-detail-label">📝 Nome da Reunião</span>
            <div class="meeting-detail-value">${meeting.title}</div>
        </div>
        
        <div class="meeting-detail-item">
            <span class="meeting-detail-label">🕐 Horário de Início (UTC)</span>
            <div class="meeting-detail-value">${startTimeStr}</div>
        </div>
        
        <div class="meeting-detail-item">
            <span class="meeting-detail-label">🕑 Horário de Término (UTC)</span>
            <div class="meeting-detail-value">${endTimeStr}</div>
        </div>
        
        <div class="meeting-detail-item">
            <span class="meeting-detail-label">👥 Participantes</span>
            <div class="meeting-detail-value">
                ${participantsHtml}
            </div>
        </div>
        
        <div class="meeting-detail-item">
            <span class="meeting-detail-label">🆔 ID da Reunião</span>
            <div class="meeting-detail-value" style="font-family: monospace; font-size: 12px;">${meeting.id}</div>
        </div>
    `;
    
    const modal = document.getElementById('meetingModal');
    modal.classList.add('show');
}

function closeMeetingModal() {
    const modal = document.getElementById('meetingModal');
    modal.classList.remove('show');
    selectedMeeting = null;
}

async function deleteMeetingFromModal() {
    if (!selectedMeeting) {
        alert('Nenhuma reunião selecionada');
        return;
    }
    
    if (!confirm(`Tem certeza que deseja excluir a reunião "${selectedMeeting.title}"?`)) {
        return;
    }
    
    const meetingTitle = selectedMeeting.title;
    
    try {
        const result = await makeRequest('DELETE', `${API_BASE}/Meetings/${selectedMeeting.id}`);
        
        if (result.status === 204) {
            currentMeetings = currentMeetings.filter(m => m.id !== selectedMeeting.id);
            closeMeetingModal();
            alert(`✅ Reunião "${meetingTitle}" excluída com sucesso!`);
            
            logRequest(`Reunião "${meetingTitle}" excluída com sucesso`, null);
            
            try {
                await loadCalendar();
                updateDashboardStats();
            } catch (updateError) {
                console.warn('Erro ao atualizar interface após exclusão (não afeta a exclusão):', updateError);
            }
            
        } else {
            throw new Error(`Erro ${result.status}: ${result.data?.message || 'Falha ao excluir reunião'}`);
        }
        
    } catch (error) {
        console.error('Erro ao excluir reunião:', error);
        alert(`❌ Erro ao excluir reunião: ${error.message}`);
        logError('Erro ao excluir reunião: ' + error.message);
    }
}

window.onclick = function(event) {
    const modal = document.getElementById('meetingModal');
    if (event.target === modal) {
        closeMeetingModal();
    }
}

document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
        const modal = document.getElementById('meetingModal');
        if (modal.classList.contains('show')) {
            closeMeetingModal();
        }
    }
});   