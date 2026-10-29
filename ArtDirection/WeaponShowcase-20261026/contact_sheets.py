import json,pathlib
from PIL import Image,ImageDraw
p=pathlib.Path(__file__).parent
m=json.loads((p/'manifest.json').read_text(encoding='utf-8-sig'))
keys=list(dict.fromkeys(s['weapon'] for s in m['shots']))
for start in range(0,len(keys),6):
    subset=keys[start:start+6]
    sheet=Image.new('RGB',(1280,len(subset)*280),(19,17,28));d=ImageDraw.Draw(sheet)
    for row,key in enumerate(subset):
        shots=[next(s for s in m['shots'] if s['weapon']==key and s['view']==v) for v in ['Weapon 1','Held 1','Holstered']]
        for col,s in enumerate(shots):
            im=Image.open(p/s['file']);im.thumbnail((410,250));x=col*426+(410-im.width)//2;y=row*280+24
            sheet.paste(im,(x,y));d.text((col*426+8,row*280+5),key+' / '+s['view'],fill='white')
    sheet.save(p/f'contact-{start//6+1}.jpg',quality=90)
print('Contact sheets made')
