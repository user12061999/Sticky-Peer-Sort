"""Package individually generated PNGs; never crop objects out of sample sheets.
Usage: python Tools/prepare_production_stickers.py Temp/sticker_sources.json
"""
import hashlib, json, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/_Game/ProductionStickers'

def prepare(sources):
    (OUT / 'Art').mkdir(parents=True, exist_ok=True)
    (OUT / 'Validation').mkdir(exist_ok=True)
    entries = []
    for source in sources:
        ident = 'sps_' + source['id']
        original = Image.open(source['source']).convert('RGBA')
        alpha = original.getchannel('A').point(lambda a: 0 if a < 8 else a)
        original.putalpha(alpha)
        box = alpha.getbbox()
        if not box: raise ValueError('Empty alpha: ' + ident)
        # Normalize one independently generated object, not a sample-sheet cutout.
        fitted = original.crop(box).convert('RGBa')
        fitted.thumbnail((448,448), Image.Resampling.LANCZOS)
        fitted = fitted.convert('RGBA')
        result = Image.new('RGBA',(512,512))
        result.paste(fitted, ((512-fitted.width)//2,(512-fitted.height)//2))
        a = result.getchannel('A').point(lambda v: 0 if v < 3 else v)
        result.putalpha(a)
        pixels = result.load()
        for y in range(512):
            for x in range(512):
                if pixels[x,y][3] == 0: pixels[x,y] = (0,0,0,0)
        path = OUT / 'Art' / (ident+'.png')
        result.save(path, optimize=True)
        bbox = a.getbbox()
        assert min(bbox[0],bbox[1],512-bbox[2],512-bbox[3]) >= 32
        assert result.mode == 'RGBA' and a.getextrema() == (0,255)
        entries.append(dict(id=ident, category=source['id'].split('_')[0], displayName=source['id'].split('_',1)[1].replace('_',' ').upper(), filename=path.name, sha256=hashlib.sha256(path.read_bytes()).hexdigest(), bbox=list(bbox), sourceSha256=hashlib.sha256(Path(source['source']).read_bytes()).hexdigest(), prompt=source['prompt']))
    assert len(entries) == len({e['id'] for e in entries})
    assert len(entries) == len({e['sha256'] for e in entries})
    (OUT / 'manifest.json').write_text(json.dumps(dict(generator='built-in image_gen; one request per sticker',width=512,height=512,format='RGBA PNG',paddingMinPixels=32,entries=entries),indent=2),encoding='utf8')
    # Proofs deliberately composite the independent PNGs only for reviewing size.
    for size in (96,160):
        cell, rows = size+36, (len(entries)+7)//8
        proof=Image.new('RGB',(cell*8,(size+58)*rows),(214,153,70)); draw=ImageDraw.Draw(proof)
        for i,e in enumerate(entries):
            im=Image.open(OUT/'Art'/e['filename']).resize((size,size),Image.Resampling.LANCZOS)
            x=(i%8)*cell+18; y=(i//8)*(size+58)+6
            proof.paste(im,(x,y),im)
            label=e['id'][4:]
            for line in range(2): draw.text((x-10,y+size+4+line*12),label[line*21:(line+1)*21],fill=(35,30,49))
        proof.save(OUT/'Validation'/f'GameplaySize_{size}.png')
    (OUT/'Validation'/'PNGValidation.txt').write_text(f'PASS: {len(entries)} individually generated, unique RGBA PNGs; all 512x512; at least 32px clear padding; transparent RGB zeroed; full alpha range; unique hashes.\nWhite die-cut borders and absence of shadows require visual review.\n',encoding='utf8')
    print('Packaged',len(entries),'stickers in',OUT)
if __name__ == '__main__': prepare(json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig')))
