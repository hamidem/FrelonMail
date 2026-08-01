const elements = {
  form: document.querySelector('#upload-form'),
  input: document.querySelector('#file-input'),
  dropZone: document.querySelector('#drop-zone'),
  dropTitle: document.querySelector('#drop-title'),
  dropSubtitle: document.querySelector('#drop-subtitle'),
  selection: document.querySelector('#file-selection'),
  fileName: document.querySelector('#file-name'),
  fileSize: document.querySelector('#file-size'),
  clearFile: document.querySelector('#clear-file'),
  analyzeButton: document.querySelector('#analyze-button'),
  formMessage: document.querySelector('#form-message'),
  emptyResult: document.querySelector('#empty-result'),
  result: document.querySelector('#incident-result'),
  historyList: document.querySelector('#history-list'),
  historyEmpty: document.querySelector('#history-empty'),
  refreshHistory: document.querySelector('#refresh-history'),
  campaignList: document.querySelector('#campaign-list'),
  campaignEmpty: document.querySelector('#campaign-empty'),
  campaignMessage: document.querySelector('#campaign-message'),
  refreshCampaigns: document.querySelector('#refresh-campaigns'),
  campaignDetail: document.querySelector('#campaign-detail'),
  applicationVersion: document.querySelector('#application-version'),
  quitApplication: document.querySelector('#quit-application'),
  shutdownScreen: document.querySelector('#shutdown-screen')
};

let selectedFile = null;
let currentIncidentId = null;
let currentCampaign = null;

for (const button of document.querySelectorAll('[data-result-view]')) {
  button.addEventListener('click', () => setResultView(button.dataset.resultView));
}

const labels = {
  risk: { Unknown: 'Non déterminé', Low: 'Faible', Medium: 'Modéré', High: 'Élevé', Critical: 'Critique' },
  vigilance: { Unknown: 'à déterminer', Low: 'faible', Medium: 'modérée', High: 'élevée', Critical: 'critique' },
  classification: {
    Unknown: 'Non classé', Spam: 'Spam', Phishing: 'Hameçonnage', Malware: 'Logiciel malveillant',
    Scam: 'Escroquerie', BrandImpersonation: 'Usurpation de marque', CredentialTheft: 'Vol d’identifiants', Suspicious: 'Suspect'
  },
  verdict: { Inconclusive: 'Éléments insuffisants', Benign: 'Message bénin', Suspicious: 'Message suspect', ConfirmedFraud: 'Fraude confirmée' },
  campaignVerdict: { Inconclusive: 'Éléments insuffisants', Rejected: 'Rapprochement rejeté', Confirmed: 'Campagne confirmée' },
  confidence: { None: '', Low: 'Confiance faible', Medium: 'Confiance modérée' },
  ioc: { Unknown: 'Inconnu', IpAddress: 'Adresse IP', Domain: 'Domaine', Url: 'URL', Email: 'Email', Hash: 'Empreinte', FileName: 'Fichier' }
};

function setSelectedFile(file) {
  elements.formMessage.textContent = '';
  if (!file) {
    selectedFile = null;
    elements.input.value = '';
    elements.selection.hidden = true;
    elements.analyzeButton.disabled = true;
    elements.dropTitle.textContent = 'Déposer le message suspect';
    elements.dropSubtitle.textContent = 'Fichier EML ou MSG · cliquer pour le sélectionner · 25 Mo maximum';
    return;
  }

  const extension = file.name.toLowerCase().split('.').pop();
  if (extension !== 'eml' && extension !== 'msg') {
    elements.formMessage.textContent = 'Ce format n’est pas accepté. Sélectionnez le fichier EML ou MSG du message, et non une pièce jointe.';
    return;
  }

  if (file.size === 0 || file.size > 25 * 1024 * 1024) {
    elements.formMessage.textContent = 'Le fichier doit faire entre 1 octet et 25 Mo.';
    return;
  }

  selectedFile = file;
  elements.fileName.textContent = file.name;
  document.querySelector('.mini-file').textContent = extension.toUpperCase();
  elements.fileSize.textContent = formatBytes(file.size);
  elements.selection.hidden = false;
  elements.analyzeButton.disabled = false;
  elements.dropTitle.textContent = 'Preuve prête à être analysée';
  elements.dropSubtitle.textContent = 'Le traitement restera strictement local';
}

elements.dropZone.addEventListener('click', () => elements.input.click());
elements.input.addEventListener('change', () => setSelectedFile(elements.input.files[0]));
elements.clearFile.addEventListener('click', () => setSelectedFile(null));

for (const eventName of ['dragenter', 'dragover']) {
  elements.dropZone.addEventListener(eventName, event => {
    event.preventDefault();
    elements.dropZone.classList.add('dragging');
  });
}

for (const eventName of ['dragleave', 'drop']) {
  elements.dropZone.addEventListener(eventName, event => {
    event.preventDefault();
    elements.dropZone.classList.remove('dragging');
  });
}

elements.dropZone.addEventListener('drop', event => {
  const file = event.dataTransfer.files[0];
  if (!file) {
    elements.formMessage.textContent = 'La messagerie n’a pas fourni de fichier. Utilisez « Comment récupérer le fichier du message ? » ci-dessous.';
    return;
  }
  setSelectedFile(file);
});

elements.form.addEventListener('submit', async event => {
  event.preventDefault();
  if (!selectedFile) return;

  setLoading(true);
  try {
    const response = await fetch('/api/incidents/analyze', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/octet-stream',
        'X-Frelon-Filename': encodeURIComponent(selectedFile.name)
      },
      body: selectedFile
    });

    if (!response.ok) throw new Error(await readError(response));
    const incident = await response.json();
    renderIncident(incident);
    setSelectedFile(null);
    await loadHistory();
    document.querySelector('#result-card').scrollIntoView({ behavior: 'smooth', block: 'center' });
  } catch (error) {
    elements.formMessage.textContent = error.message || 'L’analyse locale a échoué.';
  } finally {
    setLoading(false);
  }
});

elements.refreshHistory.addEventListener('click', loadHistory);
elements.refreshCampaigns.addEventListener('click', loadCampaigns);
document.querySelector('#campaign-close').addEventListener('click', () => {
  currentCampaign = null;
  elements.campaignDetail.hidden = true;
});

elements.quitApplication.addEventListener('click', async () => {
  if (!window.confirm('Quitter Frelon ? Les analyses enregistrées seront conservées.')) return;

  elements.quitApplication.disabled = true;
  elements.quitApplication.lastElementChild.textContent = 'Arrêt en cours…';

  try {
    const sessionResponse = await fetch('/api/application/session', { cache: 'no-store' });
    if (!sessionResponse.ok) throw new Error();
    const session = await sessionResponse.json();

    const shutdownResponse = await fetch('/api/application/shutdown', {
      method: 'POST',
      headers: { 'X-Frelon-Shutdown-Token': session.shutdownToken }
    });
    if (!shutdownResponse.ok) throw new Error();

    document.querySelector('.app-shell').hidden = true;
    elements.shutdownScreen.hidden = false;
  } catch {
    elements.quitApplication.disabled = false;
    elements.quitApplication.lastElementChild.textContent = 'Quitter Frelon';
    window.alert('Frelon n’a pas pu être arrêté. Réessayez dans quelques instants.');
  }
});

document.querySelector('#review-history-toggle').addEventListener('click', event => {
  const button = event.currentTarget;
  const content = document.querySelector('#review-history-content');
  const expanded = button.getAttribute('aria-expanded') === 'true';
  button.setAttribute('aria-expanded', String(!expanded));
  content.hidden = expanded;
});

document.querySelector('#review-verdict-select').addEventListener('change', event => {
  const confirmed = event.target.value === 'ConfirmedFraud';
  const field = document.querySelector('#classification-field');
  field.hidden = !confirmed;
  document.querySelector('#review-classification').required = confirmed;
  if (!confirmed) document.querySelector('#review-classification').value = '';
});

document.querySelector('#review-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!currentIncidentId) return;
  const incidentId = currentIncidentId;

  const verdict = document.querySelector('#review-verdict-select').value;
  const classification = verdict === 'Suspicious'
    ? 'Suspicious'
    : verdict === 'ConfirmedFraud'
      ? document.querySelector('#review-classification').value
      : null;
  const message = document.querySelector('#review-message');
  const submit = document.querySelector('#review-submit');
  message.className = 'review-message';
  message.textContent = '';
  submit.disabled = true;

  try {
    const response = await fetch(`/api/incidents/${encodeURIComponent(incidentId)}/reviews`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ verdict, classification, notes: document.querySelector('#review-notes-input').value || null })
    });
    if (!response.ok) throw new Error(await readError(response));
    await response.json();
    await loadHistory();
    if (incidentId !== currentIncidentId) return;
    await loadReviewHistory(incidentId);
    document.querySelector('#review-form').reset();
    document.querySelector('#classification-field').hidden = true;
    message.className = 'review-message success';
    message.textContent = 'Décision ajoutée à l’historique local.';
  } catch (error) {
    message.textContent = error.message || 'La décision n’a pas pu être enregistrée.';
  } finally {
    submit.disabled = false;
  }
});

document.querySelector('#campaign-review-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!currentCampaign?.currentCandidate) return;

  const reviewedFingerprint = currentCampaign.fingerprint;
  const message = document.querySelector('#campaign-review-message');
  const submit = document.querySelector('#campaign-review-submit');
  message.className = 'review-message';
  message.textContent = '';
  submit.disabled = true;

  try {
    const response = await fetch(`/api/campaigns/${encodeURIComponent(reviewedFingerprint)}/reviews`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        candidateSnapshot: currentCampaign.currentCandidate,
        verdict: document.querySelector('#campaign-verdict').value,
        notes: document.querySelector('#campaign-notes').value || null
      })
    });
    if (!response.ok) throw new Error(await readError(response));

    document.querySelector('#campaign-review-form').reset();
    await loadCampaigns();
    await openCampaign(reviewedFingerprint, false);
    message.className = 'review-message success';
    message.textContent = 'Décision ajoutée à l’historique local.';
  } catch (error) {
    message.textContent = error.message || 'La décision n’a pas pu être enregistrée.';
  } finally {
    submit.disabled = false;
  }
});

async function loadHistory() {
  elements.refreshHistory.disabled = true;
  try {
    const response = await fetch('/api/incidents');
    if (!response.ok) throw new Error('Historique indisponible');
    renderHistory(await response.json());
  } catch {
    elements.historyList.replaceChildren();
    elements.historyEmpty.hidden = false;
    elements.historyEmpty.querySelector('strong').textContent = 'Historique momentanément indisponible';
    elements.historyEmpty.querySelector('span').textContent = 'L’analyse locale reste accessible.';
  } finally {
    elements.refreshHistory.disabled = false;
  }
}

async function openIncident(incidentId) {
  try {
    const response = await fetch(`/api/incidents/${encodeURIComponent(incidentId)}`);
    if (!response.ok) throw new Error();
    renderIncident(await response.json());
    document.querySelector('#result-card').scrollIntoView({ behavior: 'smooth', block: 'center' });
  } catch {
    elements.formMessage.textContent = 'Impossible de charger cet incident local.';
  }
}

async function loadCampaigns() {
  elements.refreshCampaigns.disabled = true;
  elements.campaignMessage.textContent = '';

  try {
    const response = await fetch('/api/campaigns');
    if (!response.ok) throw new Error(await readError(response));
    renderCampaigns(await response.json());
  } catch (error) {
    elements.campaignList.replaceChildren();
    elements.campaignEmpty.hidden = false;
    elements.campaignEmpty.querySelector('strong').textContent = 'Campagnes momentanément indisponibles';
    elements.campaignEmpty.querySelector('span').textContent = 'Les incidents et leurs analyses restent accessibles.';
    elements.campaignMessage.textContent = error.message || '';
  } finally {
    elements.refreshCampaigns.disabled = false;
  }
}

async function loadApplicationInfo() {
  try {
    const response = await fetch('/api/application/info', { cache: 'no-store' });
    if (!response.ok) return;
    const identity = await response.json();
    elements.applicationVersion.textContent = `v${identity.version}`;
    elements.applicationVersion.title = `${identity.productName} ${identity.version}`;
  } catch {
    // L'identité visuelle ne doit jamais bloquer les fonctions locales.
  }
}

async function openCampaign(fingerprint, scroll = true) {
  elements.campaignMessage.textContent = '';

  try {
    const response = await fetch(`/api/campaigns/${encodeURIComponent(fingerprint)}`);
    if (!response.ok) throw new Error(await readError(response));
    renderCampaignDetails(await response.json());
    if (scroll) elements.campaignDetail.scrollIntoView({ behavior: 'smooth', block: 'start' });
  } catch (error) {
    elements.campaignMessage.textContent = error.message || 'Impossible de charger cette campagne locale.';
  }
}

function renderCampaigns(campaigns) {
  elements.campaignList.replaceChildren();
  elements.campaignEmpty.hidden = campaigns.length !== 0;

  for (const summary of campaigns) {
    const candidate = summary.candidate;
    const verdict = summary.latestReview?.verdict;
    const card = element('button', 'campaign-card');
    card.type = 'button';
    card.addEventListener('click', () => openCampaign(candidate.fingerprint));

    const top = element('div', 'campaign-card-top');
    top.append(
      element('span', '', `Empreinte ${shortFingerprint(candidate.fingerprint)}`),
      element('span', `campaign-status ${verdict || ''}`, verdict ? labels.campaignVerdict[verdict] : 'À examiner')
    );

    const bottom = element('div', 'campaign-card-bottom');
    const links = element('span');
    links.append(
      element('strong', '', candidate.links.length),
      document.createTextNode(` lien${candidate.links.length > 1 ? 's' : ''} expliqué${candidate.links.length > 1 ? 's' : ''}`)
    );
    bottom.append(links, element('span', '', 'Examiner →'));
    card.append(
      top,
      element('h3', '', `${candidate.incidentIds.length} incidents rapprochés`),
      element('p', '', campaignPeriod(candidate)),
      bottom
    );
    elements.campaignList.append(card);
  }
}

function renderCampaignDetails(details) {
  currentCampaign = details;
  const candidate = details.candidateSnapshot;
  const latestVerdict = details.latestReview?.verdict;
  elements.campaignDetail.hidden = false;

  setText('#campaign-detail-title', `Campagne ${shortFingerprint(details.fingerprint)}`);
  setText('#campaign-detail-period', `${campaignPeriod(candidate)} · empreinte complète ${details.fingerprint}`);
  setText('#campaign-incident-count', candidate.incidentIds.length);
  setText('#campaign-link-count', candidate.links.length);
  setText('#campaign-review-state', latestVerdict ? labels.campaignVerdict[latestVerdict] : 'À examiner');

  const incidents = document.querySelector('#campaign-incidents');
  incidents.replaceChildren();
  for (const incidentId of candidate.incidentIds) {
    const button = element('button', 'campaign-incident', shortId(incidentId));
    button.type = 'button';
    button.addEventListener('click', () => openIncident(incidentId));
    incidents.append(button);
  }

  const links = document.querySelector('#campaign-links');
  links.replaceChildren();
  for (const link of candidate.links) {
    const row = element('article', 'campaign-link');
    const linkedIncidents = element('div', 'campaign-link-incidents');
    linkedIncidents.append(
      element('strong', '', `${shortId(link.firstIncidentId)} ↔ ${shortId(link.secondIncidentId)}`),
      element('small', '', 'Indicateurs communs')
    );
    const matches = element('div', 'campaign-match-list');
    for (const match of link.matches) {
      matches.append(element('span', 'campaign-match', `${labels.ioc[match.type] || match.type} · ${match.value}`));
    }
    row.append(linkedIncidents, matches, element('strong', 'campaign-score', String(link.score)));
    links.append(row);
  }

  const form = document.querySelector('#campaign-review-form');
  const guidance = document.querySelector('#campaign-review-guidance');
  for (const control of form.elements) control.disabled = !details.isCurrent;
  guidance.textContent = details.isCurrent
    ? 'Confirmez, rejetez ou laissez ouverte cette hypothèse. La décision sera horodatée sans modifier le calcul.'
    : 'Cette composition n’est plus présente dans la fenêtre récente. Son historique reste consultable, mais elle ne peut plus recevoir de décision.';
  document.querySelector('#campaign-review-message').textContent = '';
  renderCampaignReviewHistory(details.reviewHistory);
}

function renderCampaignReviewHistory(reviews) {
  const list = document.querySelector('#campaign-review-history');
  const empty = document.querySelector('#campaign-review-history-empty');
  list.replaceChildren();
  empty.hidden = reviews.length !== 0;

  for (const review of reviews) {
    const item = element('li', 'campaign-review-entry');
    item.append(
      element('strong', '', labels.campaignVerdict[review.verdict] || review.verdict),
      element('p', '', review.notes || 'Aucune note ajoutée.'),
      element('time', '', formatDate(review.decidedAt))
    );
    list.append(item);
  }
}

function renderIncident(incident) {
  currentIncidentId = incident.incidentId;
  elements.emptyResult.hidden = true;
  elements.result.hidden = false;
  setText('#result-subject', incident.subject || 'Sans objet');
  setText('#result-from', incident.from ? `Expéditeur affiché · ${incident.from}` : 'Expéditeur non déterminé');
  setText('#classification', labels.classification[incident.classification] || incident.classification);
  renderGuidance(incident.guidance, incident.riskLevel);
  renderClassificationAssessment(incident.classificationAssessment);

  const score = Math.max(0, Math.min(100, Math.round(incident.riskValue)));
  const riskColor = riskColorFor(incident.riskLevel);
  const ring = document.querySelector('#score-ring');
  ring.style.setProperty('--score', score);
  ring.style.setProperty('--risk-color', riskColor);
  document.querySelector('.risk-copy').style.setProperty('--risk-color', riskColor);
  setText('#risk-score', score);
  setText('#risk-level', labels.risk[incident.riskLevel] || incident.riskLevel);
  setText('#risk-summary', riskSummary(incident.riskLevel));
  setText('#ioc-count', incident.iocs.length);
  setText('#url-count', incident.urlCount);
  setText('#attachment-count', incident.attachmentCount);
  setText('#evidence-file', incident.sourceFileName);
  setText('#evidence-hash', incident.sourceSha256 || 'Non disponible');
  setExportLinks(incident.incidentId);
  loadReviewHistory(incident.incidentId);

  renderAuthentication(incident.authentication);
  renderReasons(incident.riskReasons);
  renderDefensiveFindings(incident.defensiveFindings || []);
  renderIocs(incident.iocs);
}

function renderGuidance(guidance, riskLevel) {
  const panel = document.querySelector('#guidance-panel');
  panel.dataset.risk = (riskLevel || 'Unknown').toLowerCase();
  setText('#guidance-title', guidance.headline);
  setText('#guidance-explanation', guidance.explanation);
  setText('#guidance-level', `Vigilance : ${labels.vigilance[riskLevel] || 'à déterminer'}`);
  setText('#guidance-boundary', guidance.boundary);

  const observations = document.querySelector('#guidance-observations');
  observations.replaceChildren();
  for (const observation of guidance.keyObservations) {
    observations.append(element('li', '', observation));
  }

  const actions = document.querySelector('#guidance-actions');
  actions.replaceChildren();
  for (const action of guidance.recommendedActions) {
    actions.append(element('li', '', action));
  }
}

function setResultView(view) {
  const selectedView = view === 'expert' ? 'expert' : 'guided';
  elements.result.dataset.view = selectedView;
  for (const button of document.querySelectorAll('[data-result-view]')) {
    button.setAttribute('aria-pressed', String(button.dataset.resultView === selectedView));
  }

  if (selectedView === 'expert') {
    document.querySelector('#technical-analysis').open = true;
  }
}

async function loadReviewHistory(incidentId) {
  resetReview();
  try {
    const response = await fetch(`/api/incidents/${encodeURIComponent(incidentId)}/reviews?limit=50`);
    if (!response.ok) throw new Error();
    const decisions = await response.json();
    if (incidentId !== currentIncidentId) return;
    if (decisions.length) renderReview(decisions[0]);
    renderReviewHistory(decisions);
  } catch {
    if (incidentId === currentIncidentId) {
      setText('#review-state', 'Décision indisponible');
      setText('#review-verdict', 'Réessayer ultérieurement');
    }
  }
}

function resetReview() {
  setReviewExportsAvailable(null);
  setText('#review-state', 'Aucune décision');
  setText('#review-verdict', 'À examiner');
  setText('#review-date', '');
  const notes = document.querySelector('#review-notes');
  notes.hidden = true;
  notes.textContent = '';
  const message = document.querySelector('#review-message');
  message.className = 'review-message';
  message.textContent = '';
  renderReviewHistory([]);
}

function renderReview(decision) {
  setReviewExportsAvailable(decision);
  setText('#review-state', 'Dernière décision humaine');
  const classification = decision.verdict === 'ConfirmedFraud' && decision.classification
    ? ` · ${labels.classification[decision.classification] || decision.classification}`
    : '';
  setText('#review-verdict', `${labels.verdict[decision.verdict] || decision.verdict}${classification}`);
  setText('#review-date', formatDate(decision.decidedAt));
  const notes = document.querySelector('#review-notes');
  notes.textContent = decision.notes || '';
  notes.hidden = !decision.notes;
}

function renderReviewHistory(decisions) {
  const list = document.querySelector('#review-history-list');
  const empty = document.querySelector('#review-history-empty');
  const count = document.querySelector('#review-history-count');
  list.replaceChildren();
  count.textContent = decisions.length;
  empty.hidden = decisions.length !== 0;

  for (const [index, decision] of decisions.entries()) {
    const item = element('li', 'review-history-item');
    if (index === 0) item.classList.add('current');

    const marker = element('span', 'review-history-marker');
    const body = element('div', 'review-history-body');
    const heading = element('div', 'review-history-item-heading');
    heading.append(
      element('strong', '', labels.verdict[decision.verdict] || decision.verdict),
      element('time', '', formatDate(decision.decidedAt))
    );
    body.append(heading);

    if (decision.classification) {
      body.append(element('span', 'review-history-classification', labels.classification[decision.classification] || decision.classification));
    }
    if (decision.notes) body.append(element('p', '', decision.notes));
    item.append(marker, body);
    list.append(item);
  }
}

function setExportLinks(incidentId) {
  for (const link of document.querySelectorAll('[data-export]')) {
    const format = link.dataset.export;
    link.href = `/api/incidents/${encodeURIComponent(incidentId)}/exports/${format}`;
  }
  setReviewExportsAvailable(null);
}

function setReviewExportsAvailable(decision) {
  document.querySelector('#review-export').setAttribute('aria-disabled', decision ? 'false' : 'true');
  const validated = decision?.verdict === 'ConfirmedFraud' && Boolean(decision.classification);
  const report = document.querySelector('#validated-report-export');
  report.setAttribute('aria-disabled', validated ? 'false' : 'true');
  report.title = validated
    ? 'Signalement validé prêt à être téléchargé'
    : 'Disponible après confirmation humaine de la fraude';
}

function renderAuthentication(authentication) {
  const container = document.querySelector('#auth-pills');
  container.replaceChildren();
  for (const [name, value] of [['SPF', authentication.spf], ['DKIM', authentication.dkim], ['DMARC', authentication.dmarc]]) {
    const pill = document.createElement('span');
    const normalized = (value || 'absent').toLowerCase();
    pill.className = `auth-pill ${normalized === 'pass' ? 'pass' : normalized === 'fail' ? 'fail' : ''}`;
    pill.textContent = `${name} · ${normalized}`;
    container.append(pill);
  }
}

function renderClassificationAssessment(assessment) {
  const hasSuggestion = assessment && assessment.classification !== 'Unknown';
  const container = document.querySelector('#classification-assessment');
  container.classList.toggle('has-suggestion', hasSuggestion);
  setText(
    '#suggested-classification',
    hasSuggestion
      ? labels.classification[assessment.classification] || assessment.classification
      : 'Aucune catégorie suffisamment étayée');
  setText(
    '#classification-confidence',
    hasSuggestion ? labels.confidence[assessment.confidence] || assessment.confidence : '');

  const reasons = document.querySelector('#classification-reasons');
  reasons.replaceChildren();
  for (const reason of assessment?.reasons || []) {
    reasons.append(element('li', '', reason));
  }
}

function renderReasons(reasons) {
  const list = document.querySelector('#risk-reasons');
  list.replaceChildren();
  const values = reasons.length ? reasons : ['Aucun facteur de risque explicite détecté.'];
  for (const reason of values) {
    const item = document.createElement('li');
    item.textContent = reason;
    list.append(item);
  }
}

function renderDefensiveFindings(findings) {
  const section = document.querySelector('#defensive-section');
  const list = document.querySelector('#defensive-findings');
  section.hidden = findings.length === 0;
  list.replaceChildren();
  setText('#defensive-caption', `${findings.length} ${findings.length > 1 ? 'signaux' : 'signal'}`);

  for (const finding of findings) {
    const item = element('li', 'defensive-finding');
    const heading = element('div', 'defensive-finding-heading');
    heading.append(
      element('span', 'defensive-kind', finding.kind === 'Attachment' ? 'Pièce jointe' : 'URL'),
      element('strong', '', finding.value)
    );
    const reasons = element('ul', 'defensive-reasons');
    for (const reason of finding.reasons || []) reasons.append(element('li', '', reason));
    item.append(heading, reasons);
    list.append(item);
  }
}

function renderIocs(iocs) {
  const body = document.querySelector('#ioc-table');
  const empty = document.querySelector('#empty-iocs');
  body.replaceChildren();
  empty.hidden = iocs.length !== 0;
  document.querySelector('.table-wrap').hidden = iocs.length === 0;
  setText('#ioc-caption', `${iocs.length} indicateur${iocs.length > 1 ? 's' : ''}`);

  for (const ioc of iocs) {
    const row = document.createElement('tr');
    for (const value of [labels.ioc[ioc.type] || ioc.type, ioc.value, `${Math.round(ioc.confidence * 100)} %`, ioc.source || '—']) {
      const cell = document.createElement('td');
      cell.textContent = value;
      row.append(cell);
    }
    body.append(row);
  }
}

function renderHistory(incidents) {
  elements.historyList.replaceChildren();
  elements.historyEmpty.hidden = incidents.length !== 0;
  if (!incidents.length) return;

  for (const incident of incidents) {
    const item = document.createElement('button');
    item.type = 'button';
    item.className = 'history-item';
    item.style.setProperty('--risk-color', riskColorFor(incident.riskLevel));
    item.addEventListener('click', () => openIncident(incident.incidentId));

    const score = element('span', 'history-score', Math.round(incident.riskValue));
    const primary = element('span', 'history-primary');
    primary.append(element('strong', '', incident.sourceFileName), element('span', '', shortId(incident.incidentId)));
    const date = element('span', 'history-meta', formatDate(incident.createdAt));
    const review = element('span', `history-review ${incident.latestReviewVerdict ? 'reviewed' : 'pending'}`);
    review.append(
      element('strong', '', incident.latestReviewVerdict ? labels.verdict[incident.latestReviewVerdict] || incident.latestReviewVerdict : 'À examiner'),
      element('small', '', incident.latestReviewAt ? formatDate(incident.latestReviewAt) : 'Aucune revue humaine')
    );
    const arrow = element('span', 'history-arrow', '→');
    item.append(score, primary, date, review, arrow);
    elements.historyList.append(item);
  }
}

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function setText(selector, value) { document.querySelector(selector).textContent = value; }
function setLoading(loading) {
  elements.form.classList.toggle('loading', loading);
  elements.analyzeButton.disabled = loading || !selectedFile;
  elements.dropZone.disabled = loading;
}
function formatBytes(bytes) { return bytes < 1024 ? `${bytes} octets` : bytes < 1048576 ? `${(bytes / 1024).toFixed(1)} Ko` : `${(bytes / 1048576).toFixed(1)} Mo`; }
function formatDate(value) { return new Intl.DateTimeFormat('fr-FR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
function shortId(value) { return `Incident ${value.slice(0, 8).toUpperCase()}`; }
function shortFingerprint(value) { return value.slice(0, 12).toUpperCase(); }
function campaignPeriod(candidate) {
  const first = formatDate(candidate.firstObservedAt);
  const last = formatDate(candidate.lastObservedAt);
  return first === last ? `Observée le ${first}` : `Observée du ${first} au ${last}`;
}
function riskColorFor(level) { return ({ Low: '#3d7b65', Medium: '#8a6734', High: '#936738', Critical: '#a95854' })[level] || '#66736e'; }
function riskSummary(level) {
  return ({ Low: 'Peu de signaux préoccupants.', Medium: 'Une revue humaine est conseillée.', High: 'Plusieurs signaux demandent votre attention.', Critical: 'Priorité élevée, sans action automatique.' })[level]
    || 'Le score ne constitue pas une preuve de fraude.';
}
async function readError(response) {
  try {
    const body = await response.json();
    return body.message || body.title || 'L’analyse locale a échoué.';
  } catch { return 'L’analyse locale a échoué.'; }
}

loadHistory();
loadCampaigns();
loadApplicationInfo();
