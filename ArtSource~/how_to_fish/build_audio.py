"""Original HowToFish sound candidates; stdlib synthesis, no recordings/downloads.
All timbres, duration and mixing are project estimates, not original-game audio.
Reuses the adjacent JinxCasino build_audio.py workflow (PCM + SHA manifest),
but every sample below is newly synthesized for this Demo.
"""
import argparse, hashlib, json, math, random, struct, wave
from pathlib import Path
RATE = 24000
rng = random.Random(20261004)

def burst(seconds, low=90, high=0, decay=7, noise=.7):
    samples=[]; filtered=0
    for i in range(int(seconds*RATE)):
        t=i/RATE; u=t/seconds
        filtered=.75*filtered+.25*rng.uniform(-1,1)
        envelope=min(1,t/.004)*math.exp(-decay*u)*(1-u)
        tone=math.sin(2*math.pi*(low*t + .5*high*t*t/seconds))
        samples.append(envelope*((1-noise)*tone+noise*filtered))
    return samples

def melody(freqs, beat=.16):
    result=[0.] * int((len(freqs)*beat+.4)*RATE)
    for n,f in enumerate(freqs):
        for i in range(int((beat+.35)*RATE)):
            at=int(n*beat*RATE)+i
            if at>=len(result):break
            t=i/RATE
            result[at]+=min(1,t/.01)*math.exp(-9*t)*(math.sin(2*math.pi*f*t)+.17*math.sin(2*math.pi*f*2.01*t))
    return result

def loop(seconds, kind):
    # Integer-bin sinusoids keep the sample seam periodic; no random discontinuity.
    size=int(seconds*RATE); phases=[rng.random()*math.tau for _ in range(40)]
    bins=[rng.randrange(160 if kind=='wind' else 80, 8000 if kind=='wind' else 4500) for _ in phases]
    result=[]
    for i in range(size):
        t=i/RATE
        if kind=='motor':
            carrier=sum(math.sin(math.tau*k*40*t)/k for k in (1,2,3,5,7))
            value=carrier*(.7+.3*math.sin(math.tau*8*t)**2)
        elif kind=='reel':
            value=(math.sin(math.tau*680*t)+.3*math.sin(math.tau*1320*t))*(.1+.9*max(0,math.sin(math.tau*16*t))**10)
        else:
            carrier=sum(math.sin(math.tau*b*i/size+p) for b,p in zip(bins,phases))/len(phases)
            swell=.55+.45*math.sin(math.tau*t/seconds)**2
            if kind=='fish':swell=.18+.82*max(0,math.sin(math.tau*3*t))**4
            value=carrier*swell
        result.append(value)
    return result

def write(output, name, values, is_loop=False):
    peak=max(max(abs(v) for v in values),1e-6)
    # Peak-limited headroom, final gains are intentionally conservative in Director.
    pcm=b''.join(struct.pack('<h',round(v/peak*.58*32767)) for v in values)
    path=output/(name+'.wav')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm)
    rms=math.sqrt(sum((v/peak*.58)**2 for v in values)/len(values))
    return {'id':name,'seconds':len(values)/RATE,'loop':is_loop,'rate':RATE,'channels':1,'rms':rms,
            'peak':.58,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'source':'original stdlib synthesis; unreviewed project estimate'}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',required=True,type=Path);args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True);clips=[]
    for name,duration,kind in [('SeaLoop',8,'sea'),('WindLoop',8,'wind'),('MotorLoop',2,'motor'),('ReelLoop',2,'reel'),('FishStruggleLoop',2,'fish')]:
        clips.append(write(args.output,name,loop(duration,kind),True))
    for name,seconds,low,high,decay,noise in [
        ('Footstep',.16,90,-45,8,.82),('Splash',.6,100,-60,3,.98),('Cast',.28,600,-420,2,.9),
        ('MeleeSwing',.2,320,-220,3,.85),('GunShot',.24,130,-60,12,.92),('ShotgunShot',.46,75,-35,8,.96),
        ('RifleShot',.19,150,-75,15,.94),('Reload',.48,620,-240,5,.64),('Explosion',1.2,48,-22,5,.94),('Hit',.18,95,-45,9,.65)]:
        clips.append(write(args.output,name,burst(seconds,low,high,decay,noise)))
    for name,frequencies,beat in [('Trade',[1120,1680],.07),('UiClick',[840],.035),('BossEncounter',[110,147,165,220],.18),
                                  ('BossDefeated',[330,440,554,660],.16),('Ending',[262,330,392,523,440,392,330,523],.23)]:
        clips.append(write(args.output,name,melody(frequencies,beat)))
    (args.output/'audio-manifest.json').write_text(json.dumps(clips,indent=2),encoding='utf-8')
    print(json.dumps({'clips':len(clips),'seconds':sum(c['seconds'] for c in clips),'output':str(args.output)}))
if __name__=='__main__':main()
