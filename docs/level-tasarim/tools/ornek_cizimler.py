"""Örnek level çizimleri (CIZIM-REHBERI.md §8). Hepsi oyunda kullanılan resimlerin kaynağıdır.

Çalıştır:  python3 ornek_cizimler.py /tmp/ornekler     → PNG'ler + ornekler.png önizleme sayfası
Yeni bir resim çizerken buradaki bir fonksiyonu kopyalayıp değiştirerek başla.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixelkit import *


def papagan():
    """Kolay · yandan figür, arka plansız (~400 küp). Gövde ve kanat konturla ayrılır."""
    c = Canvas()
    OUT=(36,28,44); RED=(220,52,48); RED_D=(150,30,42); YEL=(250,204,60); BLU=(48,112,214)
    BEAK=(236,226,204); BR=(134,86,46); LEAF=(74,172,74)
    c.shape(seg([(1.5,25.5),(30.5,24.0)],1.5), BR, None, None, OUT)
    for l in (ell(5,22,2.8,1.6,-0.5), ell(28,21.2,2.8,1.5,0.6), ell(26,27.5,2.6,1.4,-0.4)): c.shape(l, LEAF, None, None, OUT)
    tail = poly([(16.5,21),(21.5,21),(25,31.5),(20,31.5)])
    c.fill(tail, BLU); c.fill(poly([(18.5,22),(20,22),(22.8,31.5),(21.6,31.5)]), RED); c.outline(tail, OUT)
    body = ell(16,15.5,6,8.6,0.25)
    c.shape(body, RED, None, RED_D, OUT)
    wing = ell(18.6,16,3.8,7,0.3)
    c.fill(wing, YEL); c.fill({p for p in wing if p[1] >= 16}, BLU); c.outline(wing, OUT)
    head = circ(13,8,5.2); c.shape(head, RED, None, RED_D, OUT)
    beak = poly([(10.2,5.4),(5.2,7.2),(5.6,12.6),(9.8,11.6)]); c.fill(beak, BEAK); c.outline(beak, OUT)
    c.fill({(6,12),(6,11),(5,11)}, OUT)
    eye = circ(12.6,7.4,1.7); c.fill(eye, BEAK); c.fill({(12,7)}, OUT)
    c.fill({(14,24),(15,24),(18,24),(19,24)}, OUT)
    return c


def oyuncak_ayi():
    """Kolay · önden figür (~460 küp). İç kulak, ağız, göbek, papyon = doğal kilitler."""
    c = Canvas()
    OUT=(40,30,36); FUR=(176,112,64); FURD=(122,72,42); CRM=(236,206,160); PNK=(236,150,150); BOW=(214,56,64); BTN=(70,120,200)
    for x in (8.5,23.5):
        e = circ(x,6.5,3.6); c.fill(e, FUR); c.outline(e, OUT); c.fill(circ(x,6.5,1.7), PNK)
    for (x,r) in ((8,0.5),(24,-0.5)):
        a = ell(x,21.5,2.6,4.4,r); c.shape(a, FUR, None, FURD, OUT)
    body = ell(16,23.5,6.8,6.6); c.shape(body, FUR, None, FURD, OUT)
    c.fill(ell(16,24.5,3.8,4.0), CRM)
    for x in (10.5,21.5):
        l = ell(x,29.2,3.0,2.3); c.fill(l, FUR); c.outline(l, OUT); c.fill(ell(x,29.4,1.5,1.1), PNK)
    head = ell(16,12,8.2,6.6); c.shape(head, FUR, None, FURD, OUT)
    c.fill(ell(16,14.6,3.6,2.5), CRM)
    c.fill(ell(16,13.4,1.5,0.9), OUT); c.fill({(16,15),(15,16),(17,16)}, OUT)
    c.fill({(12,11),(12,10),(20,11),(20,10)}, OUT)
    bow = poly([(16,18.8),(10.5,16.2),(10.5,21.4)]) | poly([(16,18.8),(21.5,16.2),(21.5,21.4)]); c.fill(bow, BOW)
    c.fill(rect(15,18,16,19), BTN)
    c.fill(circ(16,23,0.8)|circ(16,26,0.8), BTN)
    return c


def kayik_gol():
    """Orta · manzara (~860 küp). Bantlar kenara değer → kilit az; kuyruk gevşek açılış kuralı istedi."""
    c = Canvas()
    PINK=(232,98,118); ORG=(246,152,72); SUN=(255,222,112); MF=(128,84,140); MN=(70,52,98)
    WAT=(46,104,168); WL=(124,192,232); BOAT=(156,94,48); BD=(92,54,32); OUT=(30,24,40)
    c.fill(rect(0,5,31,10), PINK); c.fill(rect(0,11,31,17), ORG)
    c.fill(circ(21,16,5.5), SUN)
    far = poly([(0,17),(0,11),(4,8),(8,12),(12,9.5),(16,13),(20,15),(26,10),(31.9,14),(31.9,17)]); c.fill(far, MF)
    near = poly([(0,19),(0,14),(5,15.5),(9,13.5),(14,17),(18,19)]) | poly([(24,19),(28,15),(31.9,16),(31.9,19)]); c.fill(near, MN)
    c.fill(rect(0,18,31,31), WAT)
    for (x0,y,l) in [(17,19,8),(18,21,6),(19,23,4),(3,26,5),(24,27,6),(10,29,4),(20,30,3)]: c.fill(rect(x0,y,x0+l-1,y), WL)
    hull = poly([(5,22),(19,22),(17,25.5),(7.5,25.5)]); c.shape(hull, BOAT, None, BD, OUT)
    c.fill(rect(6,22,18,22), BD)
    person = circ(12,18.6,1.4) | rect(11,20,13,21); c.fill(person, OUT)
    c.fill(seg([(14,20.5),(19.5,27)],0.5), OUT)
    c.fill(rect(4,27,20,27) - rect(4,27,6,27), WAT)
    c.fill({(x,26) for x in range(7,18) if x%3==0}, WL)
    return c


def bonsai():
    """Orta · nesne + desenli arka plan (~870 küp). Saksı ve ağaç, arka planın içinde kilitli."""
    c = Canvas()
    PAP=(240,226,196); LAT=(150,104,70); TBL=(110,64,44); TBLL=(160,104,66); POT=(56,96,170); POTD=(34,58,112); POTL=(150,190,232)
    TR=(120,80,52); G1=(64,140,70); G2=(108,184,84); G3=(38,92,60); OUT=(36,28,40)
    c.fill(rect(0,2,31,24), PAP)
    for x in (0,8,16,24,31): c.fill(rect(x,2,x,24), LAT)
    for y in (2,9,16,24): c.fill(rect(0,y,31,y), LAT)
    c.fill(rect(0,25,31,28), TBL); c.fill(rect(0,25,31,25), LAT)
    trunk = seg([(16,23),(14,19),(17,15),(14.5,11),(16,8)],1.3) | seg([(16.5,15),(22,12.5)],0.8) | seg([(14.8,12),(9,10)],0.8)
    c.fill(trunk, TR)
    for (x,y,rx,ry) in [(9,9,4.5,2.6),(22.5,11.5,5,2.6),(16,6,5.5,3),(12,15.5,3.5,1.8)]:
        pad = ell(x,y,rx,ry); c.fill(pad, G1)
        c.fill({p for p in pad if p[1] < y-ry*0.2}, G2); c.fill({p for p in pad if p[1] > y+ry*0.4}, G3)
    pot = poly([(8,22),(24,22),(22,27),(10,27)]); c.fill(pot, POT); pass
    c.fill(rect(8,22,24,22), POTL); c.fill({(11,24),(12,24),(14,25),(15,25),(17,24),(18,24),(20,25),(21,25)}|rect(9,23,23,23), POTL); c.outline(pot, OUT)
    c.fill(rect(10,28,11,29)|rect(21,28,22,29), OUT)
    return c


def kis_kulubesi():
    """Zor · tam sahne 1024. Gece gökyüzü bantları, karlı tepeler, kulübe, ağaçlar, kardan adam."""
    c = Canvas()
    NV=(22,26,64); NV2=(40,44,98); STR=(250,236,160); SNW=(236,242,250); SNWD=(160,176,206); PIN=(30,84,70); WD=(132,74,48); WDD=(86,46,36)
    WIN=(255,206,84); SMK=(120,124,150); RED=(196,52,56)
    c.fill(ALL, NV); c.fill(band(12,20), NV2)
    for p in [(2,2),(7,5),(12,1),(17,4),(22,2),(29,5),(4,9),(26,10),(10,8),(19,9),(31,1)]: c.fill({p}, STR)
    c.fill(circ(6,4,2.2), SNW)
    c.fill(poly([(0,18),(8,14),(16,17),(24,13),(31.9,16),(31.9,31.9),(0,31.9)]), SNWD)
    c.fill(poly([(0,23),(10,20),(20,22.5),(31.9,20),(31.9,31.9),(0,31.9)]), SNW)
    for (x,b,h) in [(3,24,12),(28,23,13),(23.5,21,8),(1,20,7)]:
        tree = poly([(x-3.6,b),(x,b-h),(x+3.6,b)]); c.fill(tree, PIN)
        for k in range(3): yy=int(b-h+3+k*h/3.2); c.fill({p for p in tree if p[1]==yy}, SNW)
        c.fill(rect(x,b,x,b+1), WDD)
    house = rect(8,17,20,26); c.fill(house, WD)
    for y in range(18,27,2): c.fill(rect(8,y,20,y), WDD)
    roof = poly([(6,18),(14,10.5),(22,18)]); c.fill(roof, SNW); c.fill({p for p in roof if p[1]==17}, SNWD)
    chim = rect(16,9,18,13); c.fill(chim, RED)
    c.fill(circ(18.5,8,1.2)|circ(20,5.8,1.5)|circ(19,3,1.2), SMK)
    c.fill(rect(10,20,12,22), WIN); c.fill(rect(16,21,18,26), WDD); c.fill({(17,23)}, WIN)
    c.fill(rect(9,27,19,27), SNW)
    snowman = circ(26,26,2.6) | circ(26,22.2,1.8); c.fill(snowman, SNW); c.outline(snowman, SNWD)
    c.fill({(25,22),(27,22)}, NV); c.fill({(26,23)}, RED)
    for p in [(5,13),(13,6),(21,8),(29,12),(15,15),(2,27),(30,28)]: c.fill({p}, SNW)
    return c


def denizalti_dunyasi():
    """Zor/Orta · tam sahne 1024. Ana nesne (denizaltı) ortada ve büyük; derinlik 3 bant."""
    c = Canvas()
    W1=(70,170,206); W2=(40,116,176); W3=(24,62,118); SUB=(250,204,56); SUBD=(196,140,30); PORT=(160,226,240); OUT=(34,30,46)
    SND=(222,196,130); COR=(240,96,120); WEED=(60,160,90); FISH=(250,140,60)
    c.fill(band(0,9), W1); c.fill(band(10,19), W2); c.fill(band(20,31), W3)
    for x0 in (3,14,25): c.fill(seg([(x0,0),(x0+4,12)],0.5), PORT)
    c.fill(poly([(0,27),(9,25),(19,26.5),(31.9,24.5),(31.9,31.9),(0,31.9)]), SND)
    hull = ell(15,15,9.5,4.6); c.fill(hull, SUB); c.fill({p for p in hull if p[1] >= 17}, SUBD)
    tower = rect(11,8,17,11); c.fill(tower, SUB); c.fill(rect(11,11,17,11), SUBD)
    c.fill(rect(14,4,14,7)|rect(14,4,16,4), OUT)
    for px in (9,14,19): c.fill(circ(px+0.5,15,1.6), OUT); c.fill(circ(px+0.5,15,0.9), PORT)
    c.outline(hull|tower, OUT)
    prop = rect(24,13,25,17); c.fill(prop, OUT); c.fill(rect(26,12,26,18), SUBD)
    for (x,h) in [(3,6),(5,4),(28,7),(30,5)]:
        c.fill({(x + (1 if (y//2)%2 else 0), y) for y in range(27-h,28)}, WEED)
    for (x,y) in [(10,26),(13,25),(21,26)]: c.fill(circ(x,y,1.6)|rect(x,y,x,y+2), COR)
    for (x,y,d) in [(4,9,1),(26,6,-1),(23,23,-1)]:
        f = ell(x,y,1.8,1.0); c.fill(f, FISH); c.fill({(x-2*d,y-1),(x-2*d,y),(x-2*d,y+1)} & ALL, FISH)
    for p in [(27,10),(28,8),(29,5),(8,20),(7,18)]: c.fill({p}, PORT)
    return c


ORNEKLER = [('Papagan', papagan), ('OyuncakAyi', oyuncak_ayi), ('KayikGol', kayik_gol),
            ('Bonsai', bonsai), ('KisKulubesi', kis_kulubesi), ('DenizaltiDunyasi', denizalti_dunyasi)]


if __name__ == '__main__':
    out_dir = sys.argv[1] if len(sys.argv) > 1 else '/tmp/ornekler'
    os.makedirs(out_dir, exist_ok=True)
    items = []
    for key, fn in ORNEKLER:
        c = fn()
        path = c.save(os.path.join(out_dir, key + '.png'))
        print(key + ':', c.report().replace('\n', '\n  '))
        items.append((key, path))
    print('önizleme:', sheet(items, os.path.join(out_dir, 'ornekler.png'), cols=3))
