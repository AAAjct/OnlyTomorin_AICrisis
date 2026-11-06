from pathlib import Path
for n in ['TrialNetworkMessages.cs','TrialImpactEffects.cs']:
 p=Path('Assets/Scripts/Trial')/n;s=p.read_text(encoding='utf-8-sig');assert 'SwordReturnHit' in s;s=s.replace('SwordReturnHit','SwordFlowHit');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Trial/TrialPlayerParryArts.cs');s=p.read_text(encoding='utf-8');old=':85,65,flight.hits);';assert old in s;s=s.replace(old,':85,65,flight.hits,flight.cue==TrialCombatCue.SpiralSword);');p.write_text(s,encoding='utf-8')
