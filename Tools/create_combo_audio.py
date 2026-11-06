"""Original combo Foley and skill layers; no samples or third-party dependencies.

python Tools/create_combo_audio.py creates only the 76 new clips.
Each family has four takes with different envelopes, filter bands and pulse timing.
"""
from pathlib import Path
import math
import random
import struct
import wave

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Audio/Resources/Trial/Audio'
RATE = 44100
TAKES = 4
# duration, gain, air-envelope centre, noise low-pass, body frequency
SPECS = {
    'cutLight': (.24, .50, .40, .27, 520),
    'cutHeavy': (.46, .68, .59, .075, 105),
    'cutThrust': (.23, .50, .27, .52, 1250),
    'cutRise': (.36, .54, .52, .20, 420),
    'cutSweep': (.40, .54, .44, .12, 270),
    'cutDraw': (.31, .58, .26, .38, 1850),
    'cutSpin': (.55, .55, .44, .14, 360),
    'cutAir': (.34, .46, .56, .32, 760),
    'moveHilt': (.19, .48, .23, .17, 165),
    'moveKick': (.29, .52, .42, .065, 92),
    'skillCrimson': (.46, .61, .31, .26, 580),
    'skillMoon': (.62, .62, .45, .10, 240),
    'skillFire': (.88, .70, .27, .055, 65),
    'skillSpiral': (.74, .56, .45, .25, 890),
    'skillSakura': (.64, .50, .30, .36, 1450),
    'skillSand': (.71, .67, .18, .08, 72),
    'skillRecall': (.39, .51, .67, .22, 1100),
    'impactBody': (.20, .65, .08, .09, 78),
    'impactPierce': (.25, .66, .07, .42, 145),
}
TAU = math.tau


def pulse(t, start, decay):
    u = t - start
    return 0 if u < 0 else min(1, u / .003) * math.exp(-u * decay)


def render(key, spec, take):
    length, gain, centre, cutoff, freq = spec
    rng = random.Random(sum((i + 1) * ord(c) for i, c in enumerate(key)) * 997 + take * 8191)
    length *= (1, .94, 1.07, .98)[take]
    centre += (-.035, .025, -.012, .045)[take]
    tune = (1, 1.055, .947, 1.023)[take]
    freq *= tune
    cutoff *= (1, 1.17, .84, 1.06)[take]
    phase = rng.random() * TAU
    low = band = 0
    samples = []
    for i in range(round(RATE * length)):
        t = i / RATE
        p = t / length
        noise = rng.uniform(-1, 1)
        low += .015 * (noise - low)
        band += cutoff * (noise - band)
        high = noise - band
        air = math.exp(-((p - centre) * (5.2 if key != 'cutThrust' else 10)) ** 2)
        value = (band * 1.1 + high * .08) * air

        if key == 'cutLight':
            value += .085 * math.sin(TAU * (freq*t - 360*t*t)) * air
        elif key == 'cutHeavy':
            value += low * 2.4 * air + .18 * math.sin(TAU*(freq*t-65*t*t))*air
        elif key == 'cutThrust':
            value = (high*.28+band*.65)*air
            value += .11*math.sin(TAU*(freq*t-1800*t*t))*air
        elif key == 'cutRise':
            value += .12*math.sin(TAU*(freq*t+1350*t*t))*air + high*.12*air*p
        elif key == 'cutSweep':
            value *= .65 + .35*math.sin(math.pi*p)
            value += low*1.5*air + .08*math.sin(TAU*(freq*t-230*t*t))*air
        elif key == 'cutDraw':
            scrape = pulse(t, .008, 62)
            value += (high*.48 + .13*math.sin(TAU*freq*t))*scrape
            value += .12*math.sin(TAU*(420*t-420*t*t))*pulse(t, .06, 18)
        elif key == 'cutSpin':
            beats = sum(pulse(t, .025+j*(.13+take*.004), 23) for j in range(3))
            value = (band*1.2+high*.12)*beats + .065*math.sin(TAU*freq*t)*beats
        elif key == 'cutAir':
            value = (high*.15+band*.95)*air*(.7+.3*math.sin(TAU*8*t))
            value += .07*math.sin(TAU*(freq*t+560*t*t))*air
        elif key in ('moveHilt', 'moveKick', 'impactBody'):
            impact = pulse(t, .035 if key == 'moveKick' else .008, 32 if key == 'moveKick' else 46)
            value = (band*.85+low*1.8+math.sin(TAU*(freq*t+1.1*(1-math.exp(-t*48))))*.55)*impact
            if key != 'impactBody': value += band*air*.25
        elif key == 'impactPierce':
            value = (high*.55+band*.8)*pulse(t, .003, 80)
            value += .45*math.sin(TAU*freq*t)*pulse(t, .008, 39)
            value += band*.7*pulse(t, .034, 28)
        elif key == 'skillCrimson':
            value = (band+high*.22)*air
            value += .20*math.sin(TAU*(freq*t-460*t*t))*pulse(t, .018, 9)
            value += high*.24*pulse(t, .045, 35)
        elif key == 'skillMoon':
            value = (band*.9+low*1.7)*air
            for j, ratio in enumerate((1, 2.13, 3.76)):
                value += .12/(1+j)*math.sin(TAU*freq*ratio*t)*pulse(t, .012, 7+j*4)
        elif key == 'skillFire':
            roar = pulse(t, .018, 3.6)
            value = (low*4+band*.8+math.sin(TAU*(freq*t+2.8*(1-math.exp(-t*12))))*.27)*roar
            value += high*.17*roar*(.3+.7*max(0, math.sin(TAU*(22+take)*t+phase)))
            value += band*.75*pulse(t, .14+take*.006, 12)
        elif key == 'skillSpiral':
            orbit = (.25+.75*(.5+.5*math.sin(TAU*(8.5*t+1.7*t*t)+phase))**3)*math.sin(math.pi*p)**.7
            value = (band*.8+high*.18)*orbit
            for j, ratio in enumerate((1, 1.61, 2.73)):
                value += .09/(j+1)*math.sin(TAU*freq*ratio*t)*orbit
        elif key == 'skillSakura':
            value = 0
            for j in range(4):
                petal = pulse(t, .018+j*(.074+take*.003), 30)
                value += (band*.65+high*.12+.11*math.sin(TAU*freq*(1+j*.19)*t))*petal
        elif key == 'skillSand':
            value = (math.sin(TAU*(freq*t+1.8*(1-math.exp(-t*32))))*.45+low*2)*pulse(t, .005, 12)
            for j in range(6):
                value += (high*.18+band*.7)*pulse(t, .035+j*(.051+take*.002), 40)
        elif key == 'skillRecall':
            value = (band+high*.08)*air
            value += .12*math.sin(TAU*(freq*t-800*t*t))*air
            catch = length*.77
            value += (high*.25+.12*math.sin(TAU*2200*t))*pulse(t, catch, 55)

        value *= min(1, t/.002) * min(1, (length-t)/.018)
        samples.append(value)
    peak = max(.001, max(map(abs, samples)))
    pcm = b''.join(struct.pack('<h', round(s/peak*gain*.82*32767)) for s in samples)
    path = ROOT / f'{key}_{take}.wav'
    with wave.open(str(path), 'wb') as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm)
    return path


def generate():
    ROOT.mkdir(parents=True, exist_ok=True)
    paths = [render(key, spec, take) for key, spec in SPECS.items() for take in range(TAKES)]
    print(f'Created {len(paths)} original combo sounds ({sum(p.stat().st_size for p in paths)/1048576:.2f} MiB)')


if __name__ == '__main__':
    generate()
