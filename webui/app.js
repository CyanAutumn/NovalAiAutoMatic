/* =======================================================================
   NovalAi3 AutoMatic · WebView2 前端
   C# 宿主通过 chrome.webview.postMessage 收指令、PostWebMessageAsJson 推事件
   ======================================================================= */
(function () {
  'use strict';

  const $ = (id) => document.getElementById(id);
  const $$ = (sel, root) => Array.prototype.slice.call((root || document).querySelectorAll(sel));

  // 把输入值收进控件自身声明的 min / max（HTML 只做校验，不会自动截断手输的值）
  function clampToInput(el, value) {
    let result = value;
    const min = el.min === '' ? NaN : parseFloat(el.min);
    const max = el.max === '' ? NaN : parseFloat(el.max);
    if (!isNaN(min)) result = Math.max(min, result);
    if (!isNaN(max)) result = Math.min(max, result);
    return result;
  }

  /* ---------------- 桥接层 ---------------- */
  const bridge = {
    available: !!(window.chrome && window.chrome.webview),
    send(cmd, data) {
      if (this.available) {
        window.chrome.webview.postMessage({ cmd: cmd, data: data || {} });
      } else if (window.__mockSend) {
        window.__mockSend(cmd, data || {});
      }
    }
  };

  let state = null;
  const ui = {
    connected: false,
    theme: 'dark',
    highlight: false,
    logFilter: 'all',
    logs: [],
    directorTab: 0,
    directorBatch: false,
    selectedWildcard: null,
    selectedVibe: -1,
    vibePreview: '',
    img2imgPreview: ''
  };

  function onHostMessage(message) {
    if (!message || typeof message !== 'object') return;
    const type = message.type;
    const data = message.data || {};

    switch (type) {
      case 'state':
        state = data;
        ui.connected = true;
        ui.theme = data.theme || ui.theme;
        applyTheme();
        renderAll();
        break;
      case 'log':
        pushLog(data);
        break;
      case 'pic-info':
        $('picInfo').textContent = data.text || '';
        break;
      case 'text':
        applyTexts(data);
        break;
      case 'text-ack':
        break;
      case 'artist':
        state.artist = data;
        renderArtist();
        break;
      case 'pic':
        state.pic = data;
        renderGenSummary();
        renderParamValues();
        break;
      case 'settings':
        state.settings = data;
        renderSettingValues();
        break;
      case 'vibes':
        state.vibes = data.items || [];
        state.vibeSelected = data.selected;
        renderVibes();
        break;
      case 'vibe-pick':
        openVibePick(data);
        break;
      case 'vibe-options':
        applyVibeOptions(data);
        break;
      case 'vibe-preview':
        applyVibePreview(data);
        break;
      case 'wildcards':
        state.wildcards = data.items || [];
        state.wildcardFolder = data.folder || '';
        renderWildcards();
        break;
      case 'configs':
        state.configs = data;
        renderConfigs();
        break;
      case 'img2img':
        state.img2img = data;
        renderImg2Img();
        break;
      case 'img2img-preview':
        ui.img2imgPreview = data.url || '';
        renderImg2Img();
        break;
      case 'director':
        state.director = data;
        renderDirector();
        break;
      case 'director-preview':
        setPreviewImage('directorInput', data.url);
        break;
      case 'director-output':
        setPreviewImage('directorOutput', data.url);
        break;
      case 'director-busy':
        $('directorStatus').textContent = data.busy ? '运行中…' : '';
        $('directorRun').disabled = !!data.busy;
        break;
      case 'director-done':
        $('directorStatus').textContent = '';
        $('directorRun').disabled = false;
        if (!data.ok) toast('err', '导演工具执行失败：' + (data.message || '未知错误'));
        else toast('ok', '导演工具任务完成');
        break;
      case 'anlas':
        renderAnlas(data);
        break;
      case 'image-ready':
        showGeneratedImage(data);
        break;
      case 'iteration':
        setProgress((data.index / Math.max(1, data.total)) * 100);
        $('railState').textContent = `生成中 ${data.index}/${data.total}`;
        break;
      case 'started':
        setRunning(true);
        $('railState').textContent = '生成中';
        setProgress(3);
        break;
      case 'finished':
        setRunning(false);
        $('railState').textContent = '空闲';
        setProgress(100);
        setTimeout(() => setProgress(0), 1200);
        break;
      case 'stopped':
        setRunning(false);
        $('railState').textContent = '已停止';
        setProgress(0);
        break;
      case 'idle':
        setRunning(false);
        break;
      case 'failed':
        setRunning(false);
        setProgress(0);
        toast('err', '生成失败：' + (data.message || '未知错误'));
        break;
      case 'path-picked':
        if (pendingFolderTarget) {
          const target = pendingFolderTarget;
          pendingFolderTarget = null;
          applyFolderPick(target, data.path);
        }
        break;
      case 'tag-suggest':
        mergeSuggestions(data.items || []);
        break;
      case 'toast':
        toast(data.level || 'info', data.text || '');
        break;
      default:
        break;
    }
  }

  let pendingFolderTarget = null;

  if (bridge.available) {
    window.chrome.webview.addEventListener('message', (e) => onHostMessage(e.data));
  }

  function send(cmd, data) { bridge.send(cmd, data); }

  /* ---------------- 工具函数 ---------------- */
  function setPreviewImage(id, url) {
    const box = $(id);
    if (!box) return;
    if (!url) { return; }
    box.innerHTML = '';
    const img = document.createElement('img');
    img.src = url;
    box.appendChild(img);
  }

  function toast(level, text) {
    if (!text) return;
    const wrap = $('toastWrap');
    const div = document.createElement('div');
    div.className = 'toast ' + level;
    div.textContent = text;
    wrap.appendChild(div);
    setTimeout(() => { div.style.opacity = '0'; div.style.transition = 'opacity .3s'; }, 2600);
    setTimeout(() => wrap.removeChild(div), 3000);
  }

  function modal(opts) {
    const mask = $('modalMask');
    $('modalTitle').textContent = opts.title || '提示';
    const body = $('modalBody');
    body.innerHTML = '';
    let input = null;
    if (opts.message) {
      const p = document.createElement('div');
      p.className = 'note';
      p.style.fontSize = '13px';
      p.textContent = opts.message;
      body.appendChild(p);
    }
    if (opts.input !== undefined) {
      input = document.createElement('input');
      input.className = 'input';
      input.style.marginTop = '10px';
      input.value = opts.input || '';
      body.appendChild(input);
    }
    const foot = $('modalFoot');
    foot.innerHTML = '';
    const cancel = document.createElement('button');
    cancel.className = 'btn';
    cancel.textContent = '取消';
    cancel.onclick = () => { mask.classList.remove('show'); };
    foot.appendChild(cancel);
    const ok = document.createElement('button');
    ok.className = 'btn primary';
    ok.textContent = opts.okText || '确定';
    ok.onclick = () => {
      mask.classList.remove('show');
      if (opts.onOk) opts.onOk(input ? input.value : null);
    };
    foot.appendChild(ok);
    mask.classList.add('show');
    if (input) { input.focus(); input.select(); }
  }

  /* ---------------- 主题 ---------------- */
  function applyTheme() {
    document.documentElement.setAttribute('data-theme', ui.theme);
  }

  $('themeBtn').onclick = () => {
    ui.theme = ui.theme === 'dark' ? 'light' : 'dark';
    applyTheme();
    send('theme:set', { theme: ui.theme });
  };

  /* ---------------- 导航 ---------------- */
  $$('#rail .item').forEach((btn) => {
    btn.onclick = () => {
      $$('#rail .item').forEach((b) => b.classList.remove('active'));
      btn.classList.add('active');
      const page = btn.getAttribute('data-page');
      $$('main .page').forEach((p) => p.classList.toggle('active', p.id === page));
      btn.blur();
    };
  });

  function gotoTab(tab) {
    const btn = document.querySelector(`#directorPills button[data-tab="${tab}"]`);
    if (btn) btn.click();
  }

  /* ---------------- 窗口按钮 ---------------- */
  $('winMin').onclick = () => send('window:minimize');
  $('winMax').onclick = () => send('window:maximize');
  $('winClose').onclick = () => send('window:close');
  const titlebar = $('titlebar');
  titlebar.addEventListener('mousedown', (e) => {
    if (e.button !== 0) return;
    if (e.target.closest('button, select, input, a')) return;
    send('window:drag');
  });
  titlebar.addEventListener('dblclick', (e) => {
    if (e.target.closest('button, select, input, a')) return;
    send('window:maximize');
  });

  /* ---------------- 提示词高亮 ---------------- */
  function escapeHtml(text) {
    return String(text).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  }

  function numRanges(text) {
    const ranges = [];
    const re = /(-?\d+(?:\.\d+)?)::/g;
    let m;
    while ((m = re.exec(text)) !== null) {
      const start = m.index;
      const from = re.lastIndex;
      const end = text.indexOf('::', from);
      if (end < 0) break;
      ranges.push([start, end + 2]);
      re.lastIndex = end + 2;
    }
    return ranges;
  }

  function highlightText(text) {
    const ranges = numRanges(text);
    const inRange = (i) => ranges.some((r) => i >= r[0] && i < r[1]);
    let out = '';
    let cur = 0, sq = 0;
    let i = 0;
    while (i < text.length) {
      const ch = text[i];
      let cls = null;
      if (ch === '{') { cur++; cls = 'up'; }
      else if (ch === '}') { cls = 'up'; cur = Math.max(0, cur - 1); }
      else if (ch === '[') { sq++; cls = 'down'; }
      else if (ch === ']') { cls = 'down'; sq = Math.max(0, sq - 1); }
      else if (cur > 0) cls = 'up';
      else if (sq > 0) cls = 'down';
      if (inRange(i)) cls = 'num';
      const chEsc = escapeHtml(ch);
      out += cls ? `<span class="${cls}">${chEsc}</span>` : chEsc;
      i++;
    }
    return out;
  }

  function renderHighlight() {
    const pos = $('posPrompt').value;
    const neg = $('negPrompt').value;
    const words = pos.split(/[,，\n]/).filter((w) => w.trim().length > 0).length;
    $('posCount').textContent = words + ' 个片段';

    if (!ui.highlight) {
      $('posPreview').innerHTML = '';
      $('negPreview').innerHTML = '';
      return;
    }
    $('posPreview').innerHTML = highlightText(pos);
    $('negPreview').innerHTML = highlightText(neg);
  }

  $('posHl').onclick = () => {
    ui.highlight = !ui.highlight;
    $('posHl').classList.toggle('primary', ui.highlight);
    renderHighlight();
  };

  /* ---------------- 文本绑定 ---------------- */
  let textTimer = null;
  function bindTextarea(id, field) {
    const el = $(id);
    el.addEventListener('input', () => {
      renderHighlight();
      clearTimeout(textTimer);
      textTimer = setTimeout(() => send('text:set', { field: field, value: el.value }), 220);
    });
    el.addEventListener('blur', () => send('text:set', { field: field, value: el.value }));
  }

  bindTextarea('posPrompt', 'prompt');
  bindTextarea('negPrompt', 'negativePrompt');

  $$('[data-clear]').forEach((btn) => {
    btn.onclick = () => {
      const id = btn.getAttribute('data-clear');
      $(id).value = '';
      $(id).dispatchEvent(new Event('input'));
    };
  });

  function applyTexts(data) {
    if (!data) return;
    if (data.prompt !== undefined && document.activeElement !== $('posPrompt')) $('posPrompt').value = data.prompt;
    if (data.negativePrompt !== undefined && document.activeElement !== $('negPrompt')) $('negPrompt').value = data.negativePrompt;
    renderHighlight();
  }

  /* ---------------- 整体渲染 ---------------- */
  function renderAll() {
    $('appVersion').textContent = 'v' + (state.version || '');
    $('aboutVersion').textContent = 'v' + (state.version || '');
    $('bridgeChip').textContent = '已连接宿主';
    $('bridgeChip').className = 'chip ok';

    const token = state.settings && state.settings.Token;
    $('tokenChip').textContent = token ? 'Token 已配置' : 'Token 未配置';
    $('tokenChip').className = 'chip ' + (token ? 'ok' : 'warn');

    if (state.texts) {
      $('posPrompt').value = state.texts.prompt || '';
      $('negPrompt').value = state.texts.negativePrompt || '';
    }
    renderHighlight();

    renderGenSummary();
    renderArtist();
    renderWildcards();
    renderVibes();
    renderImg2Img();
    renderDirector();
    renderParams();
    renderSettings();
    renderConfigs();

    renderAnlas(state.anlas);
    ui.logs = [];
    $('logList').innerHTML = '';
    (state.log || []).forEach(pushLog);
    if (state.picInfo) $('picInfo').textContent = state.picInfo;
  }

  /* ---------------- 配置 ---------------- */
  function renderConfigs() {
    const cfg = state.configs || { names: [], current: '' };
    const sel = $('presetSelect');
    const names = cfg.names || [];
    // 内容没变就不重建，免得把已经展开的下拉列表顶掉
    const key = names.join('\u0001') + '\u0002' + (cfg.current || '');
    if (sel.dataset.key === key) return;
    sel.dataset.key = key;

    sel.innerHTML = '';
    if (!names.length) {
      const opt = document.createElement('option');
      opt.value = '';
      opt.textContent = '（暂无配置）';
      sel.appendChild(opt);
    }
    names.forEach((name) => {
      const opt = document.createElement('option');
      opt.value = name;
      opt.textContent = name;
      sel.appendChild(opt);
    });
    if (cfg.current) sel.value = cfg.current;
  }

  $('presetSelect').onchange = (e) => {
    if (e.target.value) send('config:load', { name: e.target.value });
  };
  // 旧版点一下下拉就会重新扫描配置目录，这里保持一致
  $('presetSelect').addEventListener('focus', () => send('config:list'));
  $('presetSave').onclick = () => {
    const name = $('presetSelect').value;
    if (!name) { $('presetSaveAs').click(); return; }
    send('config:save', { name: name });
  };
  $('presetSaveAs').onclick = () => {
    modal({
      title: '另存配置', message: '给当前参数取一个名字：', input: $('presetSelect').value || '',
      onOk: (value) => { if (value && value.trim()) send('config:save', { name: value.trim() }); }
    });
  };
  $('presetDelete').onclick = () => {
    const name = $('presetSelect').value;
    if (!name) return;
    modal({
      title: '删除配置', message: '确定删除配置「' + name + '」吗？', okText: '删除',
      onOk: () => send('config:delete', { name: name })
    });
  };
  $('presetFolder').onclick = () => send('config:openFolder');

  /* ---------------- 描述符 -> 控件 ---------------- */
  // 取值范围由 C# 侧的 PropertyRanges 提供，与旧版 PicProperty / NumericUpDown 保持一致
  function descRange(desc, key, fallback) {
    const value = desc[key];
    return (value === undefined || value === null) ? fallback : value;
  }

  function clampToDesc(value, desc) {
    let result = value;
    if (desc.min !== undefined && desc.min !== null) result = Math.max(desc.min, result);
    if (desc.max !== undefined && desc.max !== null) result = Math.min(desc.max, result);
    return result;
  }

  function descriptorControl(desc, onChange) {
    const wrap = document.createElement('label');
    wrap.className = 'field';
    wrap.dataset.prop = desc.name;
    const lbl = document.createElement('span');
    lbl.className = 'lbl';
    lbl.textContent = desc.label || desc.name;
    wrap.appendChild(lbl);

    if (desc.type === 'bool') {
      const row = document.createElement('div');
      row.className = 'row';
      row.innerHTML = '<span class="switch"><input type="checkbox"' + (desc.value ? ' checked' : '') + ' /><span class="track"></span></span>';
      row.querySelector('input').addEventListener('change', (e) => onChange(desc.name, e.target.checked));
      wrap.appendChild(row);
      return wrap;
    }

    if (desc.type === 'enum') {
      const sel = document.createElement('select');
      sel.className = 'select';
      (desc.options || []).forEach((opt) => {
        const o = document.createElement('option');
        o.value = opt.value;
        o.textContent = opt.label;
        sel.appendChild(o);
      });
      sel.value = desc.value;
      sel.addEventListener('change', () => onChange(desc.name, sel.value));
      wrap.appendChild(sel);
      return wrap;
    }

    if (desc.hint === 'multiline') {
      const ta = document.createElement('textarea');
      ta.className = 'textarea';
      ta.rows = 3;
      ta.spellcheck = false;
      ta.value = desc.value == null ? '' : desc.value;
      ta.addEventListener('change', () => onChange(desc.name, ta.value));
      wrap.appendChild(ta);
      return wrap;
    }

    if (desc.hint === 'resolution' || desc.hint === 'select') {
      const sel = document.createElement('select');
      sel.className = 'select';
      (desc.options || []).forEach((opt) => {
        const value = typeof opt === 'string' ? opt : opt.value;
        const label = typeof opt === 'string' ? opt : opt.label;
        const o = document.createElement('option');
        o.value = value;
        o.textContent = label;
        sel.appendChild(o);
      });
      sel.value = desc.value;
      sel.addEventListener('change', () => onChange(desc.name, sel.value));
      wrap.appendChild(sel);
      return wrap;
    }

    const row = document.createElement('div');
    row.className = 'row';
    const input = document.createElement('input');
    input.className = 'input grow';
    if (desc.type === 'int') { input.type = 'number'; input.step = descRange(desc, 'step', 1); }
    else if (desc.type === 'float') { input.type = 'number'; input.step = descRange(desc, 'step', 0.1); }
    else if (desc.hint === 'password') input.type = 'password';
    else input.type = 'text';
    if (desc.min !== undefined && desc.min !== null) input.min = desc.min;
    if (desc.max !== undefined && desc.max !== null) input.max = desc.max;
    input.value = desc.value == null ? '' : desc.value;
    input.addEventListener('change', () => {
      let value = input.value;
      if (desc.type === 'int') value = parseInt(value, 10) || 0;
      else if (desc.type === 'float') value = parseFloat(value) || 0;
      value = clampToDesc(value, desc);
      input.value = value;
      onChange(desc.name, value);
    });
    row.appendChild(input);

    if (desc.hint === 'folder') {
      const btn = document.createElement('button');
      btn.className = 'btn';
      btn.textContent = '…';
      btn.onclick = () => {
        pendingFolderTarget = { kind: 'param', name: desc.name };
        send('path:pickFolder', { initial: input.value });
      };
      row.appendChild(btn);
    }

    wrap.appendChild(row);
    return wrap;
  }

  function applyFolderPick(target, path) {
    if (!path) return;
    if (target.kind === 'param') send('param:set', { name: target.name, value: path });
    else if (target.kind === 'director') send('director:set', { field: 'folder', value: path });
  }

  /* ---------------- 生成参数页 ---------------- */
  function renderParams() {
    const container = $('genParamGroups');
    const groups = {};
    const order = [];
    (state.picDescriptors || []).forEach((desc) => {
      const cat = desc.category || '其他';
      if (!groups[cat]) { groups[cat] = []; order.push(cat); }
      groups[cat].push(desc);
    });

    container.innerHTML = '';
    order.forEach((cat) => {
      const title = document.createElement('div');
      title.className = 'group-title';
      title.textContent = cat;
      container.appendChild(title);
      const grid = document.createElement('div');
      grid.className = 'stack';
      groups[cat].forEach((desc) => {
        grid.appendChild(descriptorControl(desc, (name, value) => send('param:set', { name: name, value: value })));
      });
      container.appendChild(grid);
    });
  }

  function renderParamValues() {
    const container = $('genParamGroups');
    if (!state.picDescriptors || !state.pic) return;
    $$('label.field', container).forEach((label) => {
      const name = label.dataset.prop;
      if (!name || state.pic[name] === undefined) return;
      const field = label.querySelector('input, select, textarea');
      if (!field || document.activeElement === field) return;
      const value = state.pic[name];
      if (field.type === 'checkbox') field.checked = !!value;
      else if (field.value !== String(value == null ? '' : value)) field.value = value == null ? '' : value;
    });
  }

  /* ---------------- 设置页 ---------------- */
  function renderSettings() {
    const container = $('settingGroups');
    const groups = {};
    const order = [];
    (state.settingDescriptors || []).forEach((desc) => {
      const cat = desc.category || '其他';
      if (!groups[cat]) { groups[cat] = []; order.push(cat); }
      groups[cat].push(desc);
    });

    container.innerHTML = '';
    order.forEach((cat) => {
      const title = document.createElement('div');
      title.className = 'note';
      title.style.margin = '4px 0 8px';
      title.textContent = cat;
      container.appendChild(title);
      const grid = document.createElement('div');
      grid.className = 'two';
      grid.style.marginBottom = '10px';
      groups[cat].forEach((desc) => {
        grid.appendChild(descriptorControl(desc, (name, value) => send('setting:set', { name: name, value: value })));
      });
      container.appendChild(grid);
    });
  }

  function renderSettingValues() {
    const container = $('settingGroups');
    if (!state.settingDescriptors || !state.settings) return;
    $$('label.field', container).forEach((label) => {
      const name = label.dataset.prop;
      if (!name || state.settings[name] === undefined) return;
      const field = label.querySelector('input, select, textarea');
      if (!field || document.activeElement === field) return;
      const value = state.settings[name];
      if (field.type === 'checkbox') field.checked = !!value;
      else if (field.value !== String(value == null ? '' : value)) field.value = value == null ? '' : value;
    });
    const token = state.settings.Token;
    $('tokenChip').textContent = token ? 'Token 已配置' : 'Token 未配置';
    $('tokenChip').className = 'chip ' + (token ? 'ok' : 'warn');
  }

  $('settingsQueryAnlas').onclick = () => send('anlas:query', { manual: true });

  /* ---------------- 画师页 ---------------- */
  let artistBuilt = false;
  function renderArtist() {
    const a = state.artist || {};
    if (!artistBuilt) {
      artistBuilt = true;
      const bind = (id, key, isNumber, isFloat) => {
        const el = $(id);
        el.addEventListener('change', () => {
          let value = el.value;
          if (isNumber) {
            value = isFloat ? (parseFloat(value) || 0) : (parseInt(value, 10) || 0);
            value = clampToInput(el, value);
            el.value = value;
          }
          send('artist:set', { name: key, value: value });
        });
      };
      bind('artistFixed', 'fixed');
      bind('artistRandom', 'random');
      bind('artistIncreaseMax', 'increaseMax', true);
      bind('artistReduceMax', 'reduceMax', true);
      bind('artistIncreaseDcMax', 'increaseDoubleColonMax', true, true);
      bind('artistReduceDcMax', 'reduceDoubleColonMax', true, true);
      bind('artistMin', 'min', true);
      bind('artistMax', 'max', true);
      $('artistModify').addEventListener('change', () => send('artist:set', { name: 'modify', value: $('artistModify').checked }));
      $('artistInsertFixed').onclick = () => insertIntoPrompt('<固定画师>');
      $('artistInsertRandom').onclick = () => insertIntoPrompt('<随机画师>');
    }
    $('artistFixed').value = a.fixed == null ? '' : a.fixed;
    $('artistRandom').value = a.random == null ? '' : a.random;
    $('artistIncreaseMax').value = a.increaseMax;
    $('artistReduceMax').value = a.reduceMax;
    $('artistIncreaseDcMax').value = a.increaseDoubleColonMax;
    $('artistReduceDcMax').value = a.reduceDoubleColonMax;
    $('artistMin').value = a.min;
    $('artistMax').value = a.max;
    $('artistModify').checked = !!a.modify;
  }

  /* ---------------- 生图页参数摘要 ---------------- */
  function renderGenSummary() {
    const p = state.pic;
    if (!p) return;
    $('runCount').textContent = p.RunNum;
    $('chipModel').textContent = p.Model;
    $('chipMp').textContent = ((p.Width * p.Height) / 1e6).toFixed(2) + ' MP';
    $('chipSize').textContent = p.Width + 'x' + p.Height;
    $('chipSteps').textContent = p.Steps;
    $('paramSummary').textContent = p.Model + ' · ' + p.Width + 'x' + p.Height + ' · ' + p.Steps + ' 步 · ×' + p.RunNum + '（逐张队列）';
    renderParamValues();
  }

  // 底部「跑图数量」加减：数量是队列长度，每张单独请求，所以免费尺寸跑再多也不收费
  $('runMinus').onclick = () => send('param:set', { name: 'RunNum', value: Math.max(1, (parseInt(state.pic.RunNum, 10) || 1) - 1) });
  $('runPlus').onclick = () => send('param:set', { name: 'RunNum', value: (parseInt(state.pic.RunNum, 10) || 1) + 1 });
  $('openOutput').onclick = () => send('output:open');
  $('posImport').onclick = () => pickMetadataFile();

  /* ---------------- Anlas / 生成按钮 ---------------- */
  function renderAnlas(data) {
    if (!data) return;
    const formatted = (n) => (n == null ? '-' : Number(n).toLocaleString('en-US'));
    $('railAnlas').textContent = formatted(data.total);
    if (!data.tracking) {
      $('balanceChip').textContent = 'Anlas 统计已关闭';
      $('genAnlas').textContent = '';
      $('chipCost').textContent = '统计已关闭';
      return;
    }
    if (data.isV5) {
      const quota = Number(data.quotaPercent || 0).toFixed(3);
      const left = data.hasAccount ? (100 - data.usagePercent) : null;
      $('balanceChip').textContent = data.hasAccount ? ('额度 ' + data.usagePercent + '% 已用 · ' + data.tierName) : '额度 -';
      $('genAnlas').textContent = '额度 ' + quota + '% / 剩余 ' + (left == null ? '?' : left + '%');
      $('chipCost').textContent = '预计额度 ' + quota + '%';
    } else {
      const cost = (data.measured ? '' : '≈') + data.runCost;
      $('balanceChip').textContent = '剩余 ' + formatted(data.total) + ' Anlas';
      $('genAnlas').textContent = cost + ' / ' + formatted(data.total);
      $('chipCost').textContent = (data.measured ? '实测 ' : '预估 ≈') + data.runCost + ' Anlas';
    }
  }

  function setRunning(running) {
    $('genBtn').classList.toggle('running', running);
    $('genLabel').textContent = running ? '停止' : '生成';
  }

  function setProgress(percent) {
    $('progressBar').style.width = Math.max(0, Math.min(100, percent)) + '%';
  }

  function showGeneratedImage(data) {
    setPreviewImage('previewBox', data.url);
    $('previewHint').textContent = '第 ' + (data.index || 1) + ' 张 · ' + data.width + 'x' + data.height;
    $('chipSeed').textContent = data.seed;
    $('chipSteps').textContent = data.steps;
  }

  $('genBtn').onclick = () => {
    send($('genBtn').classList.contains('running') ? 'stop' : 'generate');
  };

  /* ---------------- 日志 ---------------- */
  function pushLog(data) {
    ui.logs.push(data);
    if (ui.logs.length > 500) ui.logs.shift();
    if (ui.logFilter === 'all' || matchLevel(data.level)) appendLogLine(data);
  }

  function matchLevel(level) {
    if (ui.logFilter === 'info') return level === 'info' || level === 'debug';
    if (ui.logFilter === 'warn') return level === 'warn';
    if (ui.logFilter === 'err') return level === 'err' || level === 'error' || level === 'fatal';
    return true;
  }

  function appendLogLine(data) {
    const list = $('logList');
    const atBottom = list.scrollTop + list.clientHeight >= list.scrollHeight - 30;
    const line = document.createElement('div');
    line.className = 'log-line ' + (data.level === 'error' ? 'err' : data.level);
    const time = document.createElement('span');
    time.className = 't';
    time.textContent = data.time || '';
    const msg = document.createElement('span');
    msg.className = 'msg';
    msg.textContent = data.text || '';
    line.appendChild(time);
    line.appendChild(msg);
    list.appendChild(line);
    while (list.childNodes.length > 500) list.removeChild(list.firstChild);
    if (atBottom) list.scrollTop = list.scrollHeight;
  }

  $$('#logPills button').forEach((btn) => {
    btn.onclick = () => {
      $$('#logPills button').forEach((b) => b.classList.remove('active'));
      btn.classList.add('active');
      ui.logFilter = btn.getAttribute('data-lv');
      $('logList').innerHTML = '';
      ui.logs.filter((l) => ui.logFilter === 'all' || matchLevel(l.level)).forEach(appendLogLine);
    };
  });

  $('logClear').onclick = () => {
    ui.logs = [];
    $('logList').innerHTML = '';
    send('log:clear');
  };
  $('logQueryAnlas').onclick = () => send('anlas:query', { manual: true });

  /* ---------------- wildcard ---------------- */
  let wildcardBuilt = false;
  function renderWildcards() {
    if (!wildcardBuilt) {
      wildcardBuilt = true;
      $('snippetAdd').onclick = () => send('wildcard:add', { name: $('snippetName').value, content: $('snippetValue').value });
      $('snippetUpdate').onclick = () => send('wildcard:update', { name: $('snippetName').value, content: $('snippetValue').value });
      $('snippetDelete').onclick = () => {
        if (!ui.selectedWildcard) { toast('warn', '请先选择一条片段'); return; }
        send('wildcard:delete', { name: ui.selectedWildcard });
      };
      $('wildcardReload').onclick = () => send('wildcard:reload');
      $('wildcardOpen').onclick = () => send('wildcard:openFolder');
      $('wildcardBrowse').onclick = () => {
        pendingFolderTarget = { kind: 'param', name: 'WildcardFolderPath' };
        send('path:pickFolder', { initial: $('wildcardFolder').value });
      };
      $('wildcardFolder').addEventListener('change', () => send('param:set', { name: 'WildcardFolderPath', value: $('wildcardFolder').value }));
    }

    $('wildcardFolder').value = state.wildcardFolder || '';
    const body = $('wildcardBody');
    body.innerHTML = '';
    const items = state.wildcards || [];
    $('wildcardEmpty').textContent = items.length ? '' : '没有找到 wildcard 片段文件（.txt）';
    items.forEach((item) => {
      const tr = document.createElement('tr');
      tr.className = ui.selectedWildcard === item.name ? 'sel' : '';
      const tdName = document.createElement('td');
      tdName.textContent = item.name;
      const tdValue = document.createElement('td');
      tdValue.className = 'note';
      tdValue.textContent = (item.content || '').split('\n').slice(0, 4).join(' / ');
      tr.appendChild(tdName);
      tr.appendChild(tdValue);
      tr.onclick = () => {
        ui.selectedWildcard = item.name;
        $('snippetName').value = item.name;
        $('snippetValue').value = item.content;
        renderWildcards();
        insertIntoPrompt('<' + item.name.replace(/\.txt$/i, '') + '>');
      };
      body.appendChild(tr);
    });
  }

  function escapeRegExp(text) {
    return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  // 与旧版 Tools.InsertTextToTextBox 一致：已有就删掉，没有就在光标处插入并补齐逗号
  function insertIntoPrompt(text) {
    const el = $('posPrompt');
    const value = el.value;
    const cursor = el.selectionStart == null ? value.length : el.selectionStart;
    let next;
    let caret;

    if (value.indexOf(text) >= 0) {
      // 与旧版 Tools.InsertTextToTextBox 一致：先删掉「占位符 + 尾随逗号」，再删残留的占位符
      const pattern = new RegExp(escapeRegExp(text) + '\\s*,', 'g');
      const first = new RegExp(escapeRegExp(text) + '\\s*,').exec(value);
      next = value.replace(pattern, '').split(text).join('');
      caret = first && first.index < cursor ? Math.max(0, cursor - first[0].length) : cursor;
    } else {
      const needLeft = cursor > 0 && value.charAt(cursor - 1) !== ',';
      const needRight = cursor < value.length && value.charAt(cursor) !== ',';
      const commas = (needLeft ? ',' : '') + (needRight ? ',' : '');
      let base = value;
      let pos = cursor;
      if (commas) {
        base = value.slice(0, cursor) + commas + value.slice(cursor);
        pos = cursor + (needLeft ? 1 : 0);
      }
      next = pos === 0 ? text + base : base.slice(0, pos) + text + base.slice(pos);
      caret = pos + text.length + (needRight ? 1 : 0);
    }

    el.value = next;
    el.focus();
    el.selectionStart = el.selectionEnd = Math.min(caret, next.length);
    send('text:set', { field: 'prompt', value: next });
    renderHighlight();
  }

  /* ---------------- Vibe ---------------- */
  let vibeBuilt = false;
  function renderVibes() {
    if (!vibeBuilt) {
      vibeBuilt = true;
      $('vibeAdd').onclick = () => send('vibe:pick');
      $('vibeBundle').onclick = () => send('vibe:pick');
      $('vibePreview').onclick = () => send('vibe:pick');
      $('vibeApply').onclick = () => {
        if (ui.selectedVibe < 0) return;
        send('vibe:update', {
          index: ui.selectedVibe,
          ie: parseFloat($('vibeIe').value),
          rs: parseFloat($('vibeRs').value),
          enabled: $('vibeEnabled').checked
        });
      };
      $('vibeRemove').onclick = () => {
        if (ui.selectedVibe < 0) return;
        send('vibe:delete', { index: ui.selectedVibe });
      };
      $('vibeIe').addEventListener('input', () => { $('vibeIeVal').textContent = Number($('vibeIe').value).toFixed(2); });
      $('vibeRs').addEventListener('input', () => { $('vibeRsVal').textContent = Number($('vibeRs').value).toFixed(2); });
      $('vibeIeSelect').addEventListener('change', () => {
        const value = parseFloat($('vibeIeSelect').value);
        if (isNaN(value)) return;
        $('vibeIe').value = value;
        $('vibeIeVal').textContent = Number(value).toFixed(2);
      });
    }

    if (ui.selectedVibe < 0 && state.vibeSelected >= 0) ui.selectedVibe = state.vibeSelected;
    const body = $('vibeBody');
    body.innerHTML = '';
    const items = state.vibes || [];
    $('vibeEmpty').textContent = items.length ? '' : '还没有参考图';
    items.forEach((item, index) => {
      const tr = document.createElement('tr');
      tr.className = ui.selectedVibe === index ? 'sel' : '';
      const tdOn = document.createElement('td');
      tdOn.innerHTML = '<span class="switch"><input type="checkbox"' + (item.enabled ? ' checked' : '') + ' /><span class="track"></span></span>';
      tdOn.querySelector('input').addEventListener('change', (e) => {
        send('vibe:update', { index: index, ie: item.ie, rs: item.rs, enabled: e.target.checked, name: item.name });
      });
      const tdName = document.createElement('td');
      tdName.textContent = item.name || '(未命名)';
      tdName.title = item.path || '';
      const tdIe = document.createElement('td');
      tdIe.textContent = item.ie;
      const tdRs = document.createElement('td');
      tdRs.textContent = item.rs;
      const tdDel = document.createElement('td');
      const del = document.createElement('button');
      del.className = 'btn sm danger';
      del.textContent = '移除';
      del.onclick = (e) => { e.stopPropagation(); send('vibe:delete', { index: index }); };
      tdDel.appendChild(del);
      tr.appendChild(tdOn); tr.appendChild(tdName); tr.appendChild(tdIe); tr.appendChild(tdRs); tr.appendChild(tdDel);
      tr.onclick = () => selectVibe(index, item);
      body.appendChild(tr);
    });

    const selected = ui.selectedVibe >= 0 ? items[ui.selectedVibe] : null;
    if (selected) applySelectedVibe(selected);
    else $('vibeSelName').textContent = '未选择';
  }

  function selectVibe(index, item) {
    ui.selectedVibe = index;
    send('vibe:select', { index: index });
    if (item.path && /\.(png|jpe?g|bmp|webp)$/i.test(item.path)) {
      $('vibeSelName').textContent = item.name || '';
    }
    renderVibes();
  }

  function applySelectedVibe(item) {
    $('vibeSelName').textContent = item.name || '';
    $('vibeIe').value = item.ie;
    $('vibeRs').value = item.rs;
    $('vibeIeVal').textContent = Number(item.ie).toFixed(2);
    $('vibeRsVal').textContent = Number(item.rs).toFixed(2);
    $('vibeEnabled').checked = !!item.enabled;
  }

  function openVibePick(data) {
    if (data.preview) setPreviewImage('vibePreview', data.preview);
    const options = data.options || [1];
    const defaultIe = options.length ? options[0] : 1;
    const body = $('modalBody');
    $('modalTitle').textContent = '添加参考图';
    body.innerHTML = '';
    const info = document.createElement('div');
    info.className = 'note';
    info.textContent = data.name || '';
    body.appendChild(info);
    const ieField = document.createElement('label');
    ieField.className = 'field';
    ieField.style.marginTop = '10px';
    ieField.innerHTML = '<span class="lbl">信息提取 IE</span>';
    const ieInput = document.createElement('input');
    ieInput.className = 'input';
    ieInput.type = 'number';
    ieInput.step = '0.01';
    ieInput.value = defaultIe;
    ieField.appendChild(ieInput);
    body.appendChild(ieField);
    if (options.length > 1) {
      const list = document.createElement('div');
      list.className = 'row wrap';
      options.forEach((value) => {
        const chip = document.createElement('button');
        chip.className = 'btn sm';
        chip.textContent = value;
        chip.onclick = () => { ieInput.value = value; };
        list.appendChild(chip);
      });
      body.appendChild(list);
    }
    const rsField = document.createElement('label');
    rsField.className = 'field';
    rsField.style.marginTop = '10px';
    rsField.innerHTML = '<span class="lbl">参考强度 RS</span>';
    const rsInput = document.createElement('input');
    rsInput.className = 'input';
    rsInput.type = 'number';
    rsInput.step = '0.01';
    rsInput.value = '0.6';
    rsField.appendChild(rsInput);
    body.appendChild(rsField);

    const foot = $('modalFoot');
    foot.innerHTML = '';
    const cancel = document.createElement('button');
    cancel.className = 'btn';
    cancel.textContent = '取消';
    cancel.onclick = () => $('modalMask').classList.remove('show');
    const ok = document.createElement('button');
    ok.className = 'btn primary';
    ok.textContent = '添加';
    ok.onclick = () => {
      $('modalMask').classList.remove('show');
      send('vibe:add', { path: data.path, ie: parseFloat(ieInput.value) || 1, rs: parseFloat(rsInput.value) || 0.6 });
    };
    foot.appendChild(cancel);
    foot.appendChild(ok);
    $('modalMask').classList.add('show');
  }

  function applyVibeOptions(data) {
    const options = data.options || [];
    const select = $('vibeIeSelect');
    select.innerHTML = '';
    options.forEach((value) => {
      const opt = document.createElement('option');
      opt.value = value;
      opt.textContent = value;
      select.appendChild(opt);
    });

    // .naiv4vibe 自带档位时用下拉替代滑条，和旧版的 cmbVibeIE / nudVibeIE 一致
    $('vibeIeSelectRow').hidden = options.length === 0;
    $('vibeIeRangeRow').hidden = options.length > 0;
    if (options.length) {
      const current = Number($('vibeIe').value);
      const matched = options.filter((value) => Math.abs(Number(value) - current) < 1e-6)[0];
      select.value = matched === undefined ? options[0] : matched;
    }
  }

  function applyVibePreview(data) {
    if (data.url) setPreviewImage('vibePreview', data.url);
    else $('vibePreview').innerHTML = '参考图预览';
    applyVibeOptions(data);
  }

  /* ---------------- 图生图 ---------------- */
  let img2imgBuilt = false;
  function renderImg2Img() {
    if (!img2imgBuilt) {
      img2imgBuilt = true;
      $('img2imgPick').onclick = () => send('img2img:pick');
      $('img2imgPreview').onclick = () => send('img2img:pick');
      $('img2imgClear').onclick = () => { ui.img2imgPreview = ''; send('img2img:clear'); };
      $('img2imgStrength').addEventListener('input', () => { $('img2imgStrengthVal').textContent = Number($('img2imgStrength').value).toFixed(2); });
      $('img2imgNoise').addEventListener('input', () => { $('img2imgNoiseVal').textContent = Number($('img2imgNoise').value).toFixed(2); });
      const push = () => send('img2img:set', { strength: parseFloat($('img2imgStrength').value), noise: parseFloat($('img2imgNoise').value) });
      $('img2imgStrength').addEventListener('change', push);
      $('img2imgNoise').addEventListener('change', push);
    }
    const data = state.img2img || {};
    $('img2imgName').textContent = data.name || '未选择';
    $('img2imgStrength').value = data.strength == null ? 0.01 : data.strength;
    $('img2imgNoise').value = data.noise == null ? 0 : data.noise;
    $('img2imgStrengthVal').textContent = Number($('img2imgStrength').value).toFixed(2);
    $('img2imgNoiseVal').textContent = Number($('img2imgNoise').value).toFixed(2);
    if (!ui.img2imgPreview) {
      $('img2imgPreview').innerHTML = data.path ? ('已选择：' + data.path) : '未选择图片';
    }
  }

  /* ---------------- 导演工具 ---------------- */
  let directorBuilt = false;
  function renderDirector() {
    if (!directorBuilt) {
      directorBuilt = true;
      $$('#directorPills button').forEach((btn) => {
        btn.onclick = () => {
          $$('#directorPills button').forEach((b) => b.classList.remove('active'));
          btn.classList.add('active');
          ui.directorTab = parseInt(btn.getAttribute('data-tab'), 10);
          send('director:set', { field: 'tab', value: ui.directorTab });
          renderDirectorPanes();
        };
      });
      $$('#directorMode button').forEach((btn) => {
        btn.onclick = () => {
          $$('#directorMode button').forEach((b) => b.classList.remove('active'));
          btn.classList.add('active');
          ui.directorBatch = btn.getAttribute('data-batch') === '1';
          send('director:set', { field: 'batch', value: ui.directorBatch });
        };
      });
      $('directorPickInput').onclick = () => send('director:pickInput');
      $('directorPickFolder').onclick = () => {
        pendingFolderTarget = { kind: 'director' };
        send('director:pickFolder');
      };
      $('directorOpenOutput').onclick = () => send('output:open');
      $('directorIterations').addEventListener('change', () => {
        const value = clampToInput($('directorIterations'), parseInt($('directorIterations').value, 10) || 1);
        $('directorIterations').value = value;
        send('director:set', { field: 'iterations', value: value });
      });
      $('colorizePrompt').addEventListener('change', () => send('director:set', { field: 'colorizePrompt', value: $('colorizePrompt').value }));
      $('colorizeDefry').addEventListener('change', () => send('director:set', { field: 'colorizeDefry', value: parseInt($('colorizeDefry').value, 10) }));
      $('emotionPrompt').addEventListener('change', () => send('director:set', { field: 'emotionPrompt', value: $('emotionPrompt').value }));
      $('emotionValue').addEventListener('change', () => send('director:set', { field: 'emotion', value: $('emotionValue').value }));
      $('emotionDefry').addEventListener('change', () => send('director:set', { field: 'emotionDefry', value: parseInt($('emotionDefry').value, 10) }));
      $('directorRun').onclick = () => send('director:run');
      $('directorFolder').addEventListener('change', () => send('director:set', { field: 'folder', value: $('directorFolder').value }));
    }

    const d = state.director || {};
    const fill = (id, values) => {
      const el = $(id);
      if (el.options.length !== values.length) {
        el.innerHTML = '';
        values.forEach((v, i) => {
          const o = document.createElement('option');
          o.value = i;
          o.textContent = v;
          el.appendChild(o);
        });
      }
    };
    fill('emotionValue', state.emotions || []);
    fill('emotionDefry', state.emotionDefry || []);
    fill('colorizeDefry', state.colorizeDefry || []);

    ui.directorTab = d.tab || 0;
    ui.directorBatch = !!d.batch;
    if (document.activeElement !== $('directorIterations')) $('directorIterations').value = d.iterations || 1;
    if (document.activeElement !== $('colorizePrompt')) $('colorizePrompt').value = d.colorizePrompt || '';
    if (document.activeElement !== $('emotionPrompt')) $('emotionPrompt').value = d.emotionPrompt || '';
    $('colorizeDefry').value = d.colorizeDefry || 0;
    $('emotionDefry').value = d.emotionDefry || 0;
    $('emotionValue').value = (state.emotions || []).indexOf(d.emotion) >= 0 ? (state.emotions || []).indexOf(d.emotion) : 0;
    if (document.activeElement !== $('directorFolder')) $('directorFolder').value = d.folder || '';
    $$('#directorPills button').forEach((b) => b.classList.toggle('active', parseInt(b.getAttribute('data-tab'), 10) === ui.directorTab));
    $$('#directorMode button').forEach((b) => b.classList.toggle('active', (b.getAttribute('data-batch') === '1') === ui.directorBatch));
    $('directorInput').innerHTML = d.input ? escapeHtml(d.inputName || d.input) : '输入预览';
    renderDirectorPanes();
  }

  function renderDirectorPanes() {
    $('directorColorize').classList.toggle('active', ui.directorTab === 3);
    $('directorEmotion').classList.toggle('active', ui.directorTab === 4);
    $('directorFolderRow').style.display = ui.directorBatch ? '' : 'none';
  }

  /* ---------------- 拖拽导入 / 文件选择 ---------------- */
  function pickMetadataFile() {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.png,.jpg,.jpeg,.webp';
    input.onchange = () => {
      if (input.files && input.files[0]) readMetadataFile(input.files[0]);
    };
    input.click();
  }

  function readMetadataFile(file) {
    const reader = new FileReader();
    reader.onload = () => {
      const base64 = String(reader.result).split(',')[1];
      send('metadata:import', { name: file.name, base64: base64 });
    };
    reader.readAsDataURL(file);
  }

  ['dragenter', 'dragover'].forEach((name) => {
    document.addEventListener(name, (e) => { e.preventDefault(); e.stopPropagation(); });
  });
  document.addEventListener('drop', (e) => {
    e.preventDefault();
    e.stopPropagation();
    const file = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
    if (file) readMetadataFile(file);
  });

  /* ---------------- 标签补全（对齐旧版 AutoCompleteHelper） ---------------- */
  const AC_STATIC_TAGS = ['<固定画师>', '<随机画师>', '<随机提示词>'];
  const AC_MAX_ITEMS = 40;

  let acTarget = null;
  let acList = null;
  let acTimer = null;
  let acItems = [];
  let acIndex = -1;

  function acFieldName(el) {
    return el.id === 'negPrompt' ? 'negativePrompt' : 'prompt';
  }

  function hideList() {
    if (acList) acList.style.display = 'none';
    acItems = [];
    acIndex = -1;
  }

  function hideSuggestions() {
    hideList();
    acTarget = null;
  }

  // 与旧版 AutoCompleteHelper.GetCurrentWord 一致：向前一直取到 , { } ( ) 为止
  function currentWord(el) {
    const pos = el.selectionStart;
    const text = el.value;
    let start = pos - 1;
    while (start >= 0) {
      const ch = text[start];
      if (ch === ',' || ch === '{' || ch === '}' || ch === '(' || ch === ')') break;
      start--;
    }
    start++;
    while (start < pos && /\s/.test(text[start])) start++;
    return { word: text.slice(start, pos), start: start };
  }

  // 用镜像元素量出光标位置，把候选框贴到光标下方
  function caretPosition(el) {
    const mirror = document.createElement('div');
    const cs = window.getComputedStyle(el);
    ['fontFamily', 'fontSize', 'fontWeight', 'fontStyle', 'letterSpacing', 'lineHeight',
      'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft', 'borderTopWidth',
      'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth', 'textIndent',
      'tabSize'].forEach((k) => { mirror.style[k] = cs[k]; });
    mirror.style.position = 'absolute';
    mirror.style.visibility = 'hidden';
    mirror.style.top = '0';
    mirror.style.left = '0';
    mirror.style.width = el.clientWidth + 'px';
    mirror.style.whiteSpace = 'pre-wrap';
    mirror.style.wordWrap = 'break-word';
    mirror.style.overflow = 'hidden';
    mirror.textContent = el.value.slice(0, el.selectionStart);

    const marker = document.createElement('span');
    marker.textContent = '\u200b';
    mirror.appendChild(marker);

    const host = el.parentNode || document.body;
    host.appendChild(mirror);
    const top = marker.offsetTop - el.scrollTop;
    const left = marker.offsetLeft;
    host.removeChild(mirror);
    return { top: top, left: left };
  }

  function ensureAcList(el) {
    const host = (el && el.parentNode) || document.querySelector('.ac-anchor');
    if (!host) return null;
    let list = host.querySelector(':scope > .ac-list');
    if (!list) {
      list = document.createElement('div');
      list.className = 'ac-list';
      list.style.display = 'none';
      host.appendChild(list);
    }
    acList = list;
    return list;
  }

  function buildLocalSuggestions(word) {
    const lower = word.toLowerCase();
    const seen = {};
    const items = [];
    const push = (text) => {
      if (!text || seen[text]) return;
      seen[text] = true;
      items.push(text);
    };

    AC_STATIC_TAGS.forEach((tag) => { if (tag.toLowerCase().indexOf(lower) >= 0) push(tag); });

    (state && state.wildcards ? state.wildcards : []).forEach((item) => {
      const name = '<' + String(item.name || '').replace(/\.txt$/i, '') + '>';
      if (name.toLowerCase().indexOf(lower) >= 0) push(name);
    });

    return items;
  }

  function showSuggestions(items) {
    if (!acTarget || !items || !items.length) { hideList(); return; }

    const list = ensureAcList(acTarget);
    if (!list) return;

    acItems = items;
    if (acIndex < 0 || acIndex >= items.length) acIndex = 0;

    list.innerHTML = '';
    items.forEach((item, i) => {
      const div = document.createElement('div');
      div.textContent = item;
      if (i === acIndex) div.className = 'sel';
      div.addEventListener('mousedown', (e) => {
        e.preventDefault();
        acIndex = i;
        confirmSuggestion();
      });
      list.appendChild(div);
    });

    const caret = caretPosition(acTarget);
    const lineHeight = parseFloat(window.getComputedStyle(acTarget).lineHeight) || 18;
    list.style.left = Math.max(0, acTarget.offsetLeft + caret.left) + 'px';
    list.style.top = (acTarget.offsetTop + caret.top + lineHeight + 2) + 'px';
    list.style.display = 'block';

    const selected = list.querySelector('.sel');
    if (selected && selected.scrollIntoView) selected.scrollIntoView({ block: 'nearest' });
  }

  function mergeSuggestions(items) {
    if (!acTarget || !items || !items.length) return;

    const seen = {};
    const merged = acItems.slice();
    merged.forEach((text) => { seen[text] = true; });

    let added = false;
    items.forEach((text) => {
      if (!text || seen[text]) return;
      seen[text] = true;
      merged.push(text);
      added = true;
    });

    if (added) showSuggestions(merged.slice(0, AC_MAX_ITEMS));
  }

  function confirmSuggestion() {
    if (!acTarget || acIndex < 0 || acIndex >= acItems.length) return;

    const item = acItems[acIndex];
    const el = acTarget;
    const word = currentWord(el);
    const value = el.value;

    el.value = value.slice(0, word.start) + item + value.slice(word.start + word.word.length);
    el.selectionStart = el.selectionEnd = word.start + item.length;

    renderHighlight();
    send('text:set', { field: acFieldName(el), value: el.value });
    hideSuggestions();
  }

  function requestSuggestions(el) {
    // 换到另一个输入框时，先把上一个候选框收起来，但不要清掉 acTarget 之外的请求
    if (acList && acTarget && acTarget !== el) acList.style.display = 'none';

    acTarget = el;
    acIndex = -1;

    const word = currentWord(el).word;
    if (!word) { hideList(); return; }

    showSuggestions(buildLocalSuggestions(word).slice(0, AC_MAX_ITEMS));

    // 以 "<" 开头时只找片段名，其余情况再去查标签库
    if (word.charAt(0) !== '<') send('tag:suggest', { prefix: word });
  }

  ['posPrompt', 'negPrompt'].forEach((id) => {
    const el = $(id);

    el.addEventListener('input', () => {
      clearTimeout(acTimer);
      acTimer = setTimeout(() => requestSuggestions(el), 100);
    });

    el.addEventListener('keydown', (e) => {
      const visible = !!acList && acTarget === el && acList.style.display === 'block' && acItems.length > 0;
      if (!visible) return;

      if (e.key === 'ArrowDown') {
        acIndex = (acIndex + 1) % acItems.length;
        showSuggestions(acItems);
        e.preventDefault();
      } else if (e.key === 'ArrowUp') {
        acIndex = (acIndex - 1 + acItems.length) % acItems.length;
        showSuggestions(acItems);
        e.preventDefault();
      } else if (e.key === 'Enter' || e.key === 'Tab') {
        confirmSuggestion();
        e.preventDefault();
      } else if (e.key === 'Escape') {
        hideSuggestions();
        e.preventDefault();
      }
    });

    // 失焦后如果焦点落在另一个带补全的输入框上，就交给它接管，别把它的候选框一起关掉
    el.addEventListener('blur', () => {
      setTimeout(() => {
        if (document.activeElement !== el && acTarget === el) hideSuggestions();
      }, 150);
    });
    el.addEventListener('scroll', () => { if (acTarget === el) hideList(); });
  });

  /* ---------------- 关于页 ---------------- */
  $$('.about a[data-url]').forEach((a) => {
    a.onclick = (e) => { e.preventDefault(); send('open:url', { url: a.getAttribute('data-url') }); };
  });
  $('aboutTutorial').onclick = () => send('open:url', { url: 'https://cyanautumn.github.io/NovalAi3AutoMaticDoc/' });
  $('aboutRepo').onclick = () => send('open:url', { url: 'https://github.com/CyanAutumn/NovalAi3AutoMatic' });

  /* ---------------- 启动 ---------------- */
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') { hideSuggestions(); $('modalMask').classList.remove('show'); }
    if (e.key === 'F5') e.preventDefault();
  });

  function gotoTab(page) {
    const btn = document.querySelector('#rail .item[data-page="' + page + '"]');
    if (btn) btn.click();
  }

  const hashPage = (location.hash || '').replace('#', '');
  if (hashPage) gotoTab(hashPage);
  if (bridge.available) {
    send('ready');
  } else {
    $('bridgeChip').textContent = '浏览器预览（未连接宿主）';
    $('bridgeChip').className = 'chip warn';
    if (window.__mockState) {
      state = window.__mockState();
      ui.connected = true;
      renderAll();
    }
  }})();
