import { createBank, prng } from '/src/audio/dsp.js';
import { createMixer } from '/src/audio/mixer.js';
import { createInstruments } from '/src/audio/instruments.js';
import { createMusic, compileTheme } from '/src/audio/music.js';
import { createSfx, SFX } from '/src/audio/sfx.js';
import { THEMES, CHAPTERS, STINGERS } from '/src/audio/themes.js';

const sr = 44100;
const clips = [];
const random = prng(0xca917);
Math.random = random;

async function save(name, buffer, from = 0, duration = buffer.duration - from, loop = false) {
    const offset = Math.round(from * sr), count = Math.round(duration * sr), channels = buffer.numberOfChannels;
    const wav = new ArrayBuffer(44 + count * channels * 2), view = new DataView(wav);
    const word = (at, text) => [...text].forEach((c, i) => view.setUint8(at + i, c.charCodeAt(0)));
    word(0, 'RIFF'); view.setUint32(4, wav.byteLength - 8, true); word(8, 'WAVE'); word(12, 'fmt ');
    view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, channels, true);
    view.setUint32(24, sr, true); view.setUint32(28, sr * channels * 2, true);
    view.setUint16(32, channels * 2, true); view.setUint16(34, 16, true); word(36, 'data'); view.setUint32(40, wav.byteLength - 44, true);
    const data = Array.from({ length: channels }, (_, c) => buffer.getChannelData(c));
    let peak = 0, energy = 0;
    for (let i = 0; i < count; i++) for (let c = 0; c < channels; c++) {
        let value = data[c][offset + i] || 0;
        // Noise and detuned engine oscillators are not periodic; soften only their boundary.
        if (loop && name.startsWith('engine_')) {
            const fade = Math.round(sr * 0.008);
            value *= Math.min(1, i / fade, (count - 1 - i) / fade);
        }
        peak = Math.max(peak, Math.abs(value)); energy += value * value;
        view.setInt16(44 + (i * channels + c) * 2, Math.round(Math.max(-1, Math.min(1, value)) * 32767), true);
    }
    if (peak < 0.00001 || !Number.isFinite(energy)) throw Error('Silent or invalid audio: ' + name);
    await fetch('/' + name + '.wav', { method: 'POST', body: wav });
    clips.push({ name, duration: count / sr, channels, peak, rms: Math.sqrt(energy / (count * channels)), loop });
}

function context(seconds) { return new OfflineAudioContext(2, Math.ceil(seconds * sr), sr); }
function bus(ctx, bank, kind) {
    const mixer = createMixer(ctx, bank, { bare: true });
    mixer.setVolumes(1, 1, false);
    mixer.master.gain.cancelScheduledValues(0);
    mixer.master.gain.value = 1;
    return { mixer, out: kind === 'ui' ? mixer.uiIn : mixer.sfxIn };
}

async function sfx() {
    const variants = {
        tier: [{ tier: 1 }, { tier: 2 }, { tier: 3 }],
        boost: ['drift', 'pad', 'start', 'trick', 'draft'].map(source => ({ source, duration: source === 'drift' ? 0.45 : 1 })),
        threat: [{ eta: 1.5 }, { eta: 0.5 }],
        rouletteTick: [{ pitch: 0.3 }, { pitch: 0.8 }],
        land: [{ intensity: 1 }],
        wall: [{ intensity: 1 }],
        bump: [{ intensity: 1 }],
        rustle: [{ size: 1 }],
    };
    for (const name of Object.keys(SFX)) {
        for (const [index, parameters] of (variants[name] || [{}]).entries()) {
            const ctx = context(2.5), bank = createBank(ctx);
            const { out } = bus(ctx, bank, ['click', 'pageTurn', 'pause', 'unpause'].includes(name) ? 'ui' : 'sfx');
            const s = createSfx(ctx, out, bank, random);
            if (!s.play(name, { ...parameters, minGap: 0 }, 0.02)) throw Error(name);
            const audio = await ctx.startRendering();
            // Keep tails, remove only trailing true silence.
            let last = audio.length - 1;
            const ch = audio.getChannelData(0);
            while (last > sr / 20 && Math.abs(ch[last]) < 0.00003) last--;
            await save('sfx_' + name + (variants[name] && variants[name].length > 1 ? '_' + index : ''), audio, 0.02, (last + 1) / sr - 0.02);
        }
    }
}

async function music(name, theme, options = {}) {
    const compiled = compileTheme(theme), form = options.form || theme.form;
    const tempo = options.tempo || theme.tempo, scale = options.tempoScale || 1;
    const seconds = list => list.reduce((n, section) => n + compiled.sections[section].len, 0) * 60 / (tempo * scale) / theme.steps;
    const once = !!theme.once, loopTo = options.loopTo ?? (options.form ? 0 : theme.loopTo || 0);
    const prefix = seconds(form.slice(0, loopTo)), loop = seconds(form.slice(loopTo)), start = seconds(form);
    // Two cycles preserve authored alternating lane variants and warmed reverb tails.
    const duration = once ? start + 3 : prefix + loop * 3 + 0.02;
    const ctx = context(duration), bank = createBank(ctx), { mixer } = bus(ctx, bank, 'music');
    const m = createMusic(ctx, { music: mixer.musicIn, reverb: mixer.reverbIn }, createInstruments(ctx, bank), bank);
    const instance = m.play(theme, { ...options, at: 0.02, tempo });
    // Follow the live scheduler while rendering; queuing a whole long theme creates tens of
    // thousands of simultaneously connected future voices and makes Chromium graph setup quadratic.
    const quantum = 128 / sr;
    let next = Math.ceil(0.2 / quantum) * quantum;
    let suspended = ctx.suspend(next);
    const rendering = ctx.startRendering();
    while (next < duration - 0.3) {
        await suspended;
        m.pump(ctx.currentTime, ctx.currentTime + 0.4);
        next = Math.ceil((ctx.currentTime + 0.2) / quantum) * quantum;
        suspended = next < duration - 0.3 ? ctx.suspend(next) : null;
        ctx.resume();
        if (!suspended) break;
    }
    const audio = await rendering;
    if (once) await save(name, audio, 0.02, start + 2);
    else {
        await save(name + '_start', audio, 0.02, start);
        await save(name + '_loop', audio, 0.02 + prefix + loop, loop * 2, true);
    }
    return instance;
}

async function engines() {
    const profiles = ['body', 'cpu', 'flutter', 'rattle', 'sand', 'out', 'ice', 'drift', 'drift1', 'drift2', 'drift3', 'roar', 'wind', 'shimmer'];
    for (const profile of profiles) {
        const layer = ['sand', 'out', 'ice'].includes(profile) ? 'rattle' : profile.startsWith('drift') ? 'drift' : profile;
        const { createEngines } = await import('/engine-' + layer + '.js');
        const ctx = context(4), bank = createBank(ctx), { mixer } = bus(ctx, bank, 'engine');
        const kart = { speed: ['body', 'cpu'].includes(profile) ? 0 : 10, baseTop: 25, grounded: true,
            controls: { throttle: 0.6 }, drift: { active: true, tier: +(profile.slice(-1)) || 0 },
            boostTime: 1, boostStrength: 1, draft: { charge: 1 }, invincibleTime: 1,
            surface: ['sand', 'out', 'ice'].includes(profile) ? profile : 'offroad', pos: { x: 0, y: 0, z: 0 }, index: 0 };
        if (profile === 'body' || profile === 'cpu') kart.boostTime = 0;
        // The original update reads ctx.currentTime. Proxy advances its automation clock during offline scheduling.
        const original = ctx;
        const timed = new Proxy(original, { get(target, key) {
            if (key === 'currentTime') return timed.now || 0;
            const value = Reflect.get(target, key, target); return typeof value === 'function' ? value.bind(target) : value;
        } });
        const active = createEngines(timed, mixer.engineIn, bank);
        active.setActive(true);
        for (let t = 0; t < 4; t += 1 / 60) {
            timed.now = t;
            active.update({ player: profile === 'cpu' ? null : kart, karts: profile === 'cpu' ? [kart] : [] }, { x: 0, y: 0, z: 0, rx: 1, ry: 0, rz: 0 }, 1, 1 / 60);
        }
        await save('engine_' + profile, await ctx.startRendering(), 2, 2, true);
    }
}

async function dynamicsReference() {
    const rate = 48000, frames = 16384, step = 16;
    const ctx = new OfflineAudioContext(2, frames, rate);
    const input = ctx.createBuffer(2, frames, rate), l = input.getChannelData(0), r = input.getChannelData(1);
    for (let i = 0; i < frames; i++) {
        const amp = i < 4096 ? 0.04 : i < 8192 ? 0.9 : 0.03;
        l[i] = 1.25 * amp * Math.sin(2 * Math.PI * 440 * i / rate);
        r[i] = 1.25 * amp * Math.sin(2 * Math.PI * 880 * i / rate);
    }
    l[6000] += 1;
    const source = ctx.createBufferSource(); source.buffer = input;
    const glue = ctx.createDynamicsCompressor(), limiter = ctx.createDynamicsCompressor();
    Object.assign(glue.threshold, { value: -18 }); glue.knee.value = 10; glue.ratio.value = 3; glue.attack.value = 0.005; glue.release.value = 0.2;
    limiter.threshold.value = -3; limiter.knee.value = 0; limiter.ratio.value = 20; limiter.attack.value = 0.001; limiter.release.value = 0.1;
    source.connect(glue).connect(limiter).connect(ctx.destination); source.start(0);
    const output = await ctx.startRendering();
    const outputLeft = [], outputRight = [];
    for (let i = 0; i < frames; i += step) {
        outputLeft.push(output.getChannelData(0)[i]); outputRight.push(output.getChannelData(1)[i]);
    }
    await fetch('/reference.json', { method: 'POST', body: JSON.stringify({ sampleRate: rate, step, left: [...l], right: [...r], outputLeft, outputRight }) });
}

async function boostReference() {
    const ctx = context(2.5), bank = createBank(ctx), step = 32, duration = Math.fround(1.14);
    // Native Unity decodes our PCM16 WAVs as signed integers / 32768.
    for (const name of ['white', 'crackle']) {
        const data = bank[name].getChannelData(0);
        for (let i = 0; i < data.length; i++) data[i] = Math.round(data[i] * 32767) / 32768;
    }
    const out = ctx.createGain(); out.gain.value = 0.85; out.connect(ctx.destination);
    const sfx = createSfx(ctx, out, bank, prng(1234));
    sfx.play('boost', { source: 'start', duration, minGap: 0 }, 0.02);
    const result = await ctx.startRendering();
    const frames = Math.ceil((0.85 + 0.45 * duration / 1.4 + 0.3) * sr), expected = [];
    for (let i = 0; i < frames; i += step) expected.push(result.getChannelData(0)[882 + i]);
    await fetch('/boost-reference.json', { method: 'POST', body: JSON.stringify({ duration, sampleRate: sr, seed: 1234, step, expected }) });
}

try {
    await dynamicsReference();
    await boostReference();
    if (window.location.search === '?checks') {
        await fetch('/done', { method: 'POST', body: '' });
    } else {
    await sfx();
    const noiseContext = context(3), noiseBank = createBank(noiseContext);
    await save('bank_white', noiseBank.white);
    await save('bank_crackle', noiseBank.crackle);
    const tempos = { meadow: 118, bosphorus: 96, glacier: 128, desk: 124 };
    for (const [name, theme] of Object.entries(THEMES)) await music('music_' + name, theme, { tempo: tempos[name] || theme.tempo });
    for (const [name, theme] of Object.entries(CHAPTERS)) {
        await music('music_intro_' + name, theme, { form: [theme.intro], tempo: tempos[name] });
        await music('music_final_' + name, theme, { form: theme.finalLap.form, tempo: tempos[name], tempoScale: 1.12, transpose: 2 });
        await music('sting_finalLap_' + name, STINGERS.finalLap, { transpose: theme.key + 2 - STINGERS.finalLap.key });
    }
    for (const [name, theme] of Object.entries(STINGERS)) await music('sting_' + name, theme);
    await engines();
    await fetch('/manifest.json', { method: 'POST', body: JSON.stringify({ sampleRate: sr, clips }) });
    }
} catch (error) {
    await fetch('/error', { method: 'POST', body: String(error.stack || error) });
}
