"""Original physically inspired combat sound layers. No samples or external dependencies.
Rebuild with Python 3: python Tools/create_combat_audio.py
Output: mono 44.1 kHz PCM16 WAV, deterministic three-take variations.
"""
from pathlib import Path
import math
import random
import struct
import wave

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Resources/Trial/Audio'
ROOT.mkdir(parents=True, exist_ok=True)
RATE = 44100
SPECS = {
    'swing': (.29, 'air', .65), 'hit': (.26, 'slice', .85),
    'impactHeavy': (.40, 'slice', 1), 'finish': (.60, 'slice', 1),
    'parry': (1.05, 'metal', .8), 'block': (.48, 'metal', .55),
    'hurt': (.48, 'hurt', .8), 'warning': (.8, 'charge', .45),
    'dodge': (.35, 'air', .42), 'heal': (1.1, 'chime', .5),
    'death': (1.3, 'hurt', .75), 'heavy': (.65, 'impact', .85),
    'tideImpact': (1.3, 'water', .9), 'bubbleCast': (.65, 'water', .55),
    'bubblePop': (.22, 'water', .55), 'tideCharge': (1.1, 'charge', .65),
    'charge': (.7, 'chime', .6), 'counter': (.85, 'metal', .95), 'landing': (.5, 'impact', .8),
    'heartbeat': (.58, 'heart', .38), 'footstep': (.2, 'step', .28),
}


def render(key, length, kind, gain, take):
    rng = random.Random(sum(map(ord, key)) * 103 + take * 8191)
    tune = 1 + (take-1)*.045
    low = mid = 0.0
    data = []
    for i in range(int(RATE*length)):
        t = i/RATE
        p = t/length
        n = rng.uniform(-1, 1)
        low += .018*(n-low)
        mid += .23*(n-mid)
        high = n-mid
        if kind == 'air':
            env = math.sin(math.pi*p)**1.3
            value = (mid*.9+high*.13)*env*(.5+.5*math.sin(math.pi*p))
            value += math.sin(2*math.pi*(650*t-500*t*t))*math.exp(-((p-.58)*19)**2)*.05
        elif kind == 'slice':
            # Short blade bite, compressed body thump, then a tearing release.
            # Avoid the long resonant modes that made sword hits sound like wood.
            bite = high*math.exp(-t*145)*.65 + mid*math.exp(-t*58)*1.1
            body = math.sin(2*math.pi*(72*tune*t+1.2*(1-math.exp(-t*42))))*math.exp(-t*33)*.55
            release = max(0, t-.018)
            tear = (mid*.9+high*.22)*math.exp(-release*29)*min(1,release*150)
            value = math.tanh((bite+body+tear)*1.4)
            if key != 'hit':
                value += math.sin(2*math.pi*48*t)*math.exp(-t*15)*.32
        elif kind == 'impact':
            body = math.sin(2*math.pi*(100*tune*t+2.8*(1-math.exp(-t*24))))
            value = body*math.exp(-t*17)*.6 + mid*math.exp(-t*24)*.8
            value += high*math.exp(-t*160)*.5
            for f in (430, 973, 1631):
                value += math.sin(2*math.pi*f*t*tune)*math.exp(-t*21)*.065
            if key in ('finish', 'impactHeavy', 'heavy'):
                value += math.sin(2*math.pi*49*t)*math.exp(-t*7)*.35+low*math.exp(-t*6)*.6
        elif kind == 'metal':
            value = high*math.exp(-t*180)*.5
            for j, f in enumerate((713, 1187, 1919, 3097, 5171)):
                value += math.sin(2*math.pi*f*t*tune+rng.random()*.005)*math.exp(-t*(5+j*2))*.18/(1+j*.2)
            value += mid*math.exp(-t*35)*.25
        elif kind == 'hurt':
            value = mid*math.exp(-t*10)*.6 + low*math.exp(-t*7)*.6
            value += math.sin(2*math.pi*(95*t+1.9*(1-math.exp(-t*25))))*math.exp(-t*12)*.45
            value = math.tanh(value*1.6)
        elif kind == 'water':
            value = (low*3+mid*.5)*math.exp(-t*(4 if key=='tideImpact' else 9))
            value += math.sin(2*math.pi*(70*t+2*(1-math.exp(-t*30))))*math.exp(-t*8)*.35
            for j in range(4):
                offset=.018+j*.043+take*.002
                u=max(0,t-offset)
                if t>offset:
                    value += math.sin(2*math.pi*((420+j*170)*u+1300*u*u))*math.exp(-u*38)*.14
            value += high*math.exp(-t*45)*.17
        elif kind == 'charge':
            env=math.sin(math.pi*p)**.8
            value=(low*1.5+mid*.2)*env
            value += sum(math.sin(2*math.pi*f*t*tune)*.05 for f in (110, 164.8, 233.1))*env
        elif kind == 'chime':
            value=sum(math.sin(2*math.pi*f*t*tune)*math.exp(-t*(3+j))*.2 for j,f in enumerate((523.25,784.9,1046.5,1569.8)))
        elif kind == 'heart':
            value=0
            for offset,scale in ((0,1),(.19,.65)):
                u=t-offset
                if u>=0:value += math.sin(2*math.pi*57*u)*math.exp(-u*33)*scale
        else:
            value=(mid*.9+high*.25)*math.exp(-t*55)+math.sin(2*math.pi*135*t)*math.exp(-t*42)*.25
        # No hard onset/offset clicks; master headroom is kept for overlapping voices.
        value *= min(1,t/.002)*min(1,(length-t)/.025)
        data.append(value)
    peak=max(.001,max(map(abs,data)))
    pcm=b''.join(struct.pack('<h',round(max(-1,min(1,s/peak*gain*.82))*32767)) for s in data)
    with wave.open(str(ROOT/f'{key}_{take}.wav'),'wb') as out:
        out.setnchannels(1);out.setsampwidth(2);out.setframerate(RATE);out.writeframes(pcm)


if __name__ == '__main__':
    for key,(length,kind,gain) in SPECS.items():
        for take in range(3):render(key,length,kind,gain,take)
    print(f'Created {len(SPECS)*3} original combat sounds in {ROOT}')
