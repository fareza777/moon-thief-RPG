# Tiga Rekomendasi Game Playstore Portrait dari `E:\Pixel Games Asset Master`

Analisis dibuat dengan memindai langsung folder master (jumlah file, prefab, dan isi tiap kategori
diverifikasi di disk, bukan hanya dari katalog), 17 September 2026.

---

## 1. Hasil cek master: apa yang benar-benar ada

**Isinya cuma SATU paket:** `2D Cozy RPG Pixelart — Super Retro Collection v3.1.5` (penerbit Gif).
Paket 2D Fantasy Tileset (SmallScale) ada di master lain, **tidak bisa dipakai** untuk rekomendasi ini
dan tidak dihitung di bawah.

| Kategori | Path (`Assets/Gif/Super_Retro_Collection/Resources/`) | Jumlah terverifikasi |
|---|---|---|
| Sprite PNG total | seluruh paket | **3.725** PNG (36.976 file termasuk `.meta`) |
| Prefab siap pakai | `Prefabs/` | **247** prefab (16 kategori) |
| Tile (ScriptableObject) | `Environments/TilePalette/Tiles/` | **6.201** tile (termasuk 488 pohon bertumpuk) |
| Tile versi lama | `.../Legacytiles/` | **7.424** tile |
| Autotile | `.../Autotiles/` | **430** (124 single, 106 atlas, 22 animated) |
| Update paket | `Environments/TilePalette_updates/` | 2.5.0, 2.7.0 (pantai/air, `gigantic_pack`), 2.7.4 (autotile beranimasi 32–42) |
| Hero utama | `Hero/hero/color_1..5/` | 55 sprite × 5 warna = **275**, 16 jenis animasi × 4 arah |
| Karakter Action RPG | `ARPG/character_0..31/` | **2.464** sprite (32 karakter × 77 sheet) |
| Karakter peta/NPC | `Characters/Characters/` | **32** `chara_0..31.png` + atlas 16×20 & 32×32 |
| Karakter bertani | `Characters/Farm/` | **25** (walk 16×20 & 32×32, hoe, shovel, watering, atlas) |
| Hewan | `Characters/Animals/` | **123** sprite, 7 spesies (babi 102 file/6 warna, burung 8, kucing 5, tikus 3, rubah 2, kelinci 2, penguin 1) |
| Monster peta | `Characters/Monsters/` | **43** sheet (set 01–05 + 6 bonus depan-saja) |
| Battler layar tempur | `Battlers/` | **57** PNG (15 keluarga: Slime A–H, Slimesword A–H, Zombi A–D, dll.) |
| Latar pertarungan | `Backgrounds/` | **11** (Desert A–B, Dungeon A–D, Forest A–C, Plain A–B) |
| Tanaman | `Prefabs/Crops/` | **22** prefab dengan tahap tumbuh + `SpriteSelectFrame.cs` + README |
| Animasi objek | `Animations/` | Chest 7, Cristal 27, Door 18, Farm 25 (ikon tani 16×16 ×2, `farm_plant_18x32` ×21, sabit 48×48), Fire 3, Kart 5, Lamp 18, Lava 5, Switch 3, Trap 3, Water 6 |
| Prefab berperilaku | `Prefabs_with_behavior/` | 3 (peti terbuka saat disentuh, rumput tinggi bereaksi, balok bisa didorong) |
| Contoh scene | `Samples/` | 8 (`farm`, `forest`, `village`, `indoor`, `marketplace`, `gigantic_tree`, `prefabs`, `behavior`) |
| Tool | `Gif/Ruccho/FangAutoTile/` | Fang Auto Tile v2.1.0 (autotile format WOLF RPG Editor) |

### Lubang yang menentukan desain game (penting)

Hasil `find` untuk audio dan UI: **NOL file `.wav` / `.mp3` / `.ogg`**, **tidak ada** sprite UI,
panel, tombol, atau font. Satu-satunya ikon adalah 2 sheet ikon tani 16×16.

Artinya master ini **hanya grafis gameplay**. Tiga konsep di bawah tidak boleh mengandalkan aset UI,
dan `HUD/tombol/ikon/font/musik/SFX` harus dibuat atau dibeli terpisah. Penerbit yang sama menjual
**Cozy RPG Music Bundle** secara terpisah — itu pilihan paling murah untuk menjaga nada audio seragam.

---

## 2. Resep supaya "seluruh aset" benar-benar terpakai: struktur HUB + EKSPEDISI + TEMPUR

Aset paket ini mengelompok rapi jadi tiga lapis. Setiap game portrait yang saya rekomendasikan di
bawah memakai pola yang sama: **satu hub yang digarap dalam (desa/tani) + satu loop tempur + satu
lapisan dunia ubin**. Tanpa pola ini, salah satu kelompok besar (biasanya 2.464 sprite ARPG atau
57 battler) pasti jadi aset mati.

| Lapis | Isi | Peran di game |
|---|---|---|
| **Lapis Dunia** | 6.201 + 7.424 tile, 430 autotile, pantai/air, pohon bertumpuk, pohon raksasa, FangAutoTile | Peta, dungeon, biome, dekorasi lanskap |
| **Lapis Desa & Tani** | 247 prefab, 22 tanaman, 25 sprite bertani, 123 hewan, 32 NPC, animasi Chest/Door/Lamp/Water/Farm | Hub, ekonomi, progresi jangka panjang |
| **Lapis Tempur** | 275 hero, 2.464 ARPG, 43 monster, 57 battler, 11 background | Pertarungan, bos, karakter yang bisa dimainkan |

**Trik kuncinya:** jangan jadikan semua lapis sebagai "gameplay utama". Jadikan Desa sebagai **hub**
(kaya konten, nyaman di portrait), Tempur sebagai **loop harian**, Dunia sebagai **topeng dua-duanya**.

---

## 3. Aturan teknis portrait yang berlaku untuk ketiga konsep

Setting aset di master sudah benar (16 Pixels Per Unit, Point filter, tanpa kompresi). Yang harus
diatur di project game:

**Resolusi.** Pakai **Pixel Perfect Camera (URP)**, Assets PPU **16**. Reference resolution portrait
harus kelipatan 16:

| Aspect | Reference resolution | Ortho size kamera | Cocok untuk |
|---|---|---|---|
| 9:16 | **288×512** (rekomendasi) atau 270×480 | 16 unit | HP modern paling umum |
| 1:2 | **320×640** | 20 unit | HP layar jangkung 9:19.5+, paling lega untuk dungeon |
| 3:5 | 240×400 | 12,5 unit | HP lama, UI terasa besar |

Pakai **Crop Frame** untuk 9:16 dan siapkan ubin ekstra di sisi kiri/kanan, atau **Stretch Fill**
untuk 1:2. Jangan pakai upscale bebas — pixel art akan pecah tidak seragam.

**Orientasi & safe area.** `ProjectSettings` master masih belum Portrait
(`defaultScreenOrientation: 4`). Set **Portrait** di Player Settings project game. Sisakan
**±6% atas** untuk notch dan **±8% bawah** untuk gesture bar; taruh tombol aksi hanya di zona
bawah-tengah yang aman.

**Input sentuh.** `com.unity.inputsystem` 1.19 sudah terpasang dan `activeInputHandler: 2 (Both)`,
jadi `PlayerMovement.cs` bawaan paket (Input lama) tetap jalan **tanpa diedit** — jangan edit script
di master. Untuk kontrol jempol pakai **EnhancedTouch** / `OnScreenStick` dari Input System baru:
- *Tap-to-move* atau *drag-anywhere joystick* → paling pas untuk tani & JRPG.
- *Fixed virtual stick* → untuk roguelite aksi.

**UI.** `com.unity.ugui` 2.0 (termasuk TextMeshPro) + 2D Tilemap Extras 6.0.2 sudah ada. Bikin panel
9-slice sendiri dari `Resources/color_palette.png` resmi paket supaya warnanya serasi.

**Jebakan ukuran build (paling sering bikin game kena 1 GB).** Semua aset ada di dalam folder
`Resources`, jadi kalau pakai Cara A (junction) ke master, **3.725 PNG + ±14.000 tile asset ikut ke
build**. Untuk pengembangan pakai Cara A, tapi **sebelum rilis wajib pindah ke Cara B** (salin hanya
yang dipakai, keluarkan dari `Resources`). Ingat dua script yang bergantung `Resources.LoadAll`
(`CharacterAppearance.cs`, `Prefabs/Crops/Scripts/SpriteSelectFrame.cs`) — kalau spritesheetnya
dikeluarkan dari `Resources`, ganti ke referensi langsung.

**Android.** IL2CPP + ARM64, target API 34+. Kompresi tekstur untuk pixel art: **Crunch off**, dan
pakai **Sprite Atlas** untuk mengemas tile yang sering dipakai — jangan kompresi lossy, sprite akan
berdarah. Matikan `Tilemap Collider 2D` di area luas; pakai collider per-chunk.

**Performa portrait.** Layar jangkung berarti lebih banyak ubin terlihat → lebih banyak draw call.
Wajib: chunk tilemap 32×32, object pooling untuk monster/panah/partikel, dan batasi kamera pandang
±16×20 unit.

---

## 4. REKOMENDASI 1 — JRPG turn-based monster-tamer portrait
**Nama kerja: "Ubin Bayangan" (Tilebound)**

**Genre & posisi di Play Store.** JRPG turn-based single-player offline, satu tangan, sesi 3–8 menit.
Referensi pasar: Dragon Quest / Final Fantasy versi mobile, Pokémon-lite, Monster Sanctuary.
Kategori: *Role Playing*. Rating: semua umur. Ini layout **paling terbukti di portrait**: 2/3 layar
atas untuk arena (memakai 11 background bawaan), 1/3 bawah untuk baris komando jempol.

**Kenapa portrait menang di sini.** Dungeon ubin bergaya koridor vertikal terasa alami; daftar
perintah jadi kolom besar (Attack / Weapon / Skill / Item / Capture / Guard) yang enak dijempol;
monster battler bawaan paket **menghadap depan** — persis sudut pandang kamera portrait.

**Core loop (5 langkah).**
1. **Pagi di desa (hub):** garap kebun kecil (bahan ramuan & pakan), kelola 7 spesies ternak, ambil
   quest dari 32 NPC.
2. **Ekspedisi:** pilih biome — Hutan, Padang, Pantai, atau Gua; tiap biome punya set lantai sendiri.
3. **Dungeon ubin:** monster terlihat berjalan di peta (bukan random encounter buta) dan menyergap.
4. **Pertarungan turn-based:** party 3–4 karakter ARPG melawan battler; monster yang kalah bisa
   **ditangkap** dan jadi anggota party.
5. **Naik lapisan gua → bos → kembali ke desa → upgrade kebun & pandai besi.**

**Pemetaan aset (ini yang membuat cakupannya hampir penuh).**

| Aset master | Dipakai sebagai | Cakupan |
|---|---|---|
| `Battlers/` 57 PNG | 57 monster lawan **dan** 57 kandidat tangkapan. Keluarga berhuruf A–H = **tier evolusi** (SlimeA→SlimeH). Nyaris tanpa desain baru. | 100% |
| `Backgrounds/` 11 | 11 tema arena/lantai: Desert, Dungeon, Forest, Plain | 100% |
| `ARPG/character_0..31` 2.464 sprite | 32 **class hero** yang bisa dimainkan, langsung terbagi dari jenis senjata sheet-nya: pedang (sword01–03), tombak (spear01–02), sihir (staff01–06), slash (efek serangan), dan `walk_shields_1–5` (pose bertahan/pakai perisai) | 100% |
| `Hero/hero/color_1..5` 275 | Tokoh utama cerita + 4 skin warna | 100% |
| `Characters/Monsters/` 43 sheet | Monster roaming **di peta dungeon** (bukan di layar tempur) | 100% |
| `Characters/Characters/chara_0..31` | 32 NPC desa, pemberi quest, pedagang | 100% |
| `Environments/TilePalette` 13.625 tile + 430 autotile | 4 dungeon (RuleTile untuk lantai/dinding/air) + desa + jalan autotile | ~80% (sisanya varian/sudut) |
| `stacked_trees`, `gigantic_pack`, `2.7.0` pantai | Pohon raksasa = arena bos dunia terbuka; pantai = biome ketiga | 100% |
| `Prefabs/` 247 | Hiasan dungeon (Torch 17, Fire 6, Statues 3, Columns 10, Books 7, Crate 8, Barrel 5, Rock 46, Pot 27) + desa (Houses 26, Lamp 16, Torii 6) | 100% |
| `Animations/` | Peti 7 (hadiah), Cristal 27 (titik simpan/MP), Door 18 (pintu lantai), Lamp 18, Lava 5 (hazard), Water 6, Fire 3, Switch 3 (tuas), Trap 3 (jebakan), Kart 5 (kereta tambang antar lantai) | 100% |
| `Prefabs_with_behavior/` 3 | Puzzle dungeon: peti terbuka saat disentuh, rumput tinggi (sembunyi dari monster), balok dorong | 100% |
| `Crops/` 22 + `Animations/Farm` 25 + ikon tani | Side-loop kebun: hasil panen = bahan ramuan; ikon 16×16 langsung dipakai di UI inventori | 100% |
| `Characters/Farm` 25 + `Animals` 123 | Animasi bertani (cangkul, sekop, siram) + 7 spesies ternak/piaraan yang memberi buff | 100% |
| FangAutoTile | Autotile air sungai/pantai dan jalan setapak | opsional |

**Cakupan total: ~100% kategori, ±95% file.** Hanya ±20% tile yang tersisa (varian sudut/warna yang
tidak perlu karena RuleTile sudah menangani transisi).

**Spesifikasi khas concept ini.** Pertarungan = sprite battler statis + animasi hero di baris bawah
(sheet hero punya attack/bow/throw/shield_walk/shield_block/hit/death — cukup untuk umpan balik aksi).
Dungeon: kamera ortho **16 unit** (288×512), grid 1×1, tap-to-move per ubin. Kecepatan main dibatasi
oleh giliran, jadi aman dari masalah "rasa kontrol sentuh" — inilah alasan risiko teknisnya paling
rendah dari tiga konsep.

**Monetisasi.** Gratis + iklan berhadiah di titik simpan, IAP "hapus iklan", paket dungeon tambahan,
kosmetik warna hero (5 warna sudah tersedia → gampang dijual ulang sebagai skin).

**MVP realistis 5–6 minggu.** 1 biome (Forest), 12 battler, 1 desa kecil, 4 hero class, dungeon 5
lantai, save JSON, UI TMP dasar. Setelah itu tinggal menambah biome, battler, dan dungeon — semuanya
murni konten dari aset yang sudah ada.

**Risiko utama.** Bukan teknis, tapi **penulisan konten**: dialog, deskripsi skill, 57 statistik
monster, kurva level. Siapkan tabel data (ScriptableObject/CSV) sejak hari pertama.

---

## 5. REKOMENDASI 2 — Cozy farm portrait + pertahanan malam
**Nama kerja: "Ladang Malam" (Nightfall Harvest)**

**Genre & posisi.** Life-sim bertani satu tangan dengan fase pertahanan malam. Referensi:
Stardew Valley (versi satu jempol) + Vampire Survivors saat malam + keramahan Hay Day.
Kategori: *Simulation* / *Casual*. Target pasar terbesar dan paling tahan lama di Play Store.

**Kenapa portrait cocok.** Petak kebun **9 kolom × 16 baris** masuk utuh dalam satu layar — pemain
melihat seluruh ladang tanpa menggeser kamera. Toolbar alat berdiri tegak di bawah (cangkul, sekop,
penyiram, sabit), jam & uang di atas. Ini genre yang paling "native" di portrait.

**Core loop harian.** Pagi: bajak → tanam 22 jenis tanaman → siram → rawat 7 spesies ternak →
panen → jual di pasar desa (36 px scene `marketplace_sample` bisa jadi acuan tata ruang). Siang:
belanja benih, upgrade rumah/gudang/pagar. Malam: **monster menyerang ladang** dari 43 sheet monster
dan 57 battler; tanaman yang hancur hilang → pemain bertahan pakai jebakan, obor, air, dan penjaga
sewaan. Pagi berikutnya: hitung untung.

**Ini konsep yang paling cerdas memakan aset ARPG.** 2.464 sprite ARPG tidak masuk sebagai hero yang
dimainkan, tapi sebagai **32 penjaga sewaan** — tiap karakter punya tipe senjata dari namanya sendiri
(pedang/tombak/tongkat), lengkap dengan animasi serang dan jalan-bawa-perisai. Pemain menyewa
beberapa per malam. Hampir tidak ada studio kecil yang bisa punya 32 unit berbeda dengan animasi
sebanyak itu; paket ini memberikannya gratis.

| Kelompok aset | Peran di game | Cakupan |
|---|---|---|
| `Crops/` 22 + `Animations/Farm/farm_plant_*` 21 + `farm_icons` | 22 tanaman, 5 tahap tumbuh, ikon UI inventori & toko | 100% |
| `Characters/Farm` 25 | Animasi alat pemain (hoe, shovel, watering) 4 arah, 16×20 & 32×32 | 100% |
| `Characters/Animals` 123 (7 spesies) | Ternak (babi 6 warna, kucing, kelinci, tikus) & ungags; penguin/rubah sebagai hewan eksotis langka | 100% |
| `Prefabs/` 247 | Seluruh desa: Houses 26 (rumah warga & rumah pemain), Trees 32, Rocks 46, Pots 27 (tempat jamu), Potted plants 15, Crates 8, Barrels 5, Books 7, Columns 10, Statues 3, Torii 6, Lamps 16, Torches 17, Fires 6 | 100% |
| `Animations/` | Sprinkler (Water 6), pintu rumah (Door 18), lampu (Lamp 18), api unggun (Fire 3), peti gudang (Chest 7), jebakan malam (Trap 3), lava/kolam hazard (Lava 5), tuas gerbang (Switch 3), kereta gerobak (Kart 5), cristal hiasan (Cristal 27) | 100% |
| `Prefabs_with_behavior/` 3 | Rumput tinggi (sembunyikan telur/temuan), peti terbuka, balok dorong (puzzle pemindahan batu) | 100% |
| `Characters/Monsters` 43 + `Battlers` 57 | Gelombang malam: monster peta mengambil ladang, battler sebagai elite/bos mingguan | 100% |
| `Backgrounds/` 11 | Layar ringkasan malam & latar kejadian khusus — 11 tema | 100% |
| `ARPG/character_0..31` | 32 penjaga sewaan dengan 5 tipe senjata | 100% |
| `Hero/hero/color_1..5` 275 | Petani utama: idle, walk, run, attack, bow, throw, **carry** (angkat & bawa panen), hit, death | 100% |
| Tile 13.625 + 430 autotile + pantai/pohon raksasa | Lahan, sungai, jalan desa, hutan pinggir, kebun raksasa sebagai area akhir | ~80% |
| `CharacterAppearance.cs` + `chara_0..31` | 32 warga desa yang muncul acak dengan pakaian berbeda | 100% |

**Cakupan total: ~100% kategori.** Konsep paling menyeluruh dari ketiganya.

**Spesifikasi khas.** Kamera ortho **12–20 unit**; ladang sendiri pakai **RuleTile** untuk tanah
gersang/subur + grid snap 1 unit sehingga penempatan tanaman presisi. Fase malam tidak memerlukan
kontrol rumit: **auto-serangan** untuk penjaga, pemain cukup memindahkan karakter utama dan menekan
tombol perangkap. Pooling wajib (bisa ada 60+ entitas malam). Simpan sebagai JSON dengan sistem
"hari ke-N" supaya bisa uji cepat.

**Monetisasi.** Gratis + iklan berhadiah ("percepat pagi"), IAP premium "hapus iklan + gratiskan
semua benih", kosmetik ladang (Torii, patung, lampu-lampu berbeda — aset sudah ada semua).

**MVP realistis 6–8 minggu.** Ladang 9×16 dengan 6 tanaman, 7 ternak, 1 desa, 3 jenis gelombang
malam, 1 penjaga ARPG, siklus pagi/malam, jual-beli, save.

**Risiko utama.** Dua genre yang harus seimbang (kalau ekonomi tani terlalu mudah, fase malam jadi
formalitas; kalau terlalu sulit, pemain casual kabur). Mulai dari sistem ekonomi yang **sederhana**
(5 angka: uang, hari, energi, panen, reputasi) dan jangan menambah 20 mata uang.

---

## 6. REKOMENDASI 3 — Roguelite aksi portrait satu jempol
**Nama kerja: "Gua Vertikal" (Deepblade Vertical)**

**Genre & posisi.** Action roguelite turun-gu a, satu jempol, run 5–10 menit, meta-progresi desa.
Referensi: Archero, Vampire Survivors, Hades-lite. Kategori: *Action* / *Roguelike*. Retensi harian
tinggi, tapi butuh tuning "rasa" yang paling lama dari ketiganya.

**Kenapa portrait cocok.** Koridor vertikal: bahaya datang dari **atas**, ruang reaksi ada di depan
mata, dan ujung tombak jempol ada di bagian bawah layar. Peta memanjang ke atas memberi kesan "turun
makin dalam" tanpa perlu kamera lebar; 1/5 bawah layar jadi HUD skill + peta mini.

**Core loop run.** Pilih hero (satu dari **32 karakter ARPG**) dan senjata (pedang/tombak/tongkat;
hero utama menambah **busur** dari sheet bow) → turun lantai gua prosedural → bertarung, buka peti,
hindari lava & jebakan, tarik tuas, naiki kereta tambang → lawan elite → bos → bawa pulang emas &
bahan → **upgrade desa**.

**Setiap kelompok aset dapat pekerjaan yang benar-benar cocok, bukan dipaksa.**

| Aset | Dipakai sebagai | Cakupan |
|---|---|---|
| `ARPG/character_0..31` 2.464 | 32 hero dengan tipe senjata bawaan; sheet serangan 6-frame sudah pas untuk **auto-attack**; `walk_shields_1–5` untuk pose menahan/blok | 100% |
| `Hero/hero` 275 (idle, walk, run, attack, bow, throw, lift, hit, spin, death, dead) | Hero default dengan gerak paling lengkap, termasuk **spin** (skill putar) dan **throw** (bom) | 100% |
| `Characters/Monsters` 43 | Musuh merayap di lantai gua, bergerombol di koridor | 100% |
| `Battlers` 57 | **Bos & elite per lantai**: sprite besar menghadap depan, ditambah goyangan idle + bayangan + partikel → terasa hidup tanpa animasi baru | 100% |
| `Backgrounds` 11 | 11 tema lantai (Dungeon A–D untuk gua, Forest/Desert/Plain untuk zona dunia) | 100% |
| Tile 13.625 + 430 autotile | Gua prosedural per chunk dengan RuleTile: dinding, lantai, air, lava, pantai | ~80% |
| `Prefabs` 247 | Hiasan gua (Rock 46 luar biasa untuk variasi dinding, Columns 10, Statues 3, Barrel 5, Crate 8, Books 7, Pot 27) + desa permukaan (Houses 26, Torii 6, Trees 32) | 100% |
| `Animations` | Jebakan (Trap 3), lava (Lava 5), tuas (Switch 3), peti (Chest 7), pintu lantai berikutnya (Door 18), penanda simpan (Cristal 27), kereta tambang cepat (Kart 5), air terjun (Water 6) | 100% |
| `Prefabs_with_behavior` 3 | Balok dorong = puzzle ruangan; peti = hadiah; rumput tinggi = ruang sembunyi | 100% |
| `Crops` 22 + Farm + `Animals` 123 | Meta-ekonomi desa: kebun menghasilkan emas pasif di antara run, ternak memberi buff permanen, animasi bertani jadi **cutscene hasil upgrade** | 100% |
| `chara_0..31` | 32 NPC desa: pandai besi, penjual ramuan, pemberi misi harian | 100% |
| `gigantic_pack` | Arena bos akhir di bawah pohon raksasa | 100% |

**Cakupan total: ~100% kategori.**

**Spesifikasi khas.** Kamera ortho **20 unit** (320×640) supaya koridor terlihat jauh ke atas.
Kontrol: **drag-anywhere** (jari di mana saja = arah gerak) + auto-attack, jadi tidak perlu tombol
virtual yang mengganggu pixel art. Konfigurasi paket yang sudah terbukti untuk genre ini: senjata
bisa dibuat dari sheet `slash01` (efek tebasan) dan `arrow` (anak panah proyektil).
Gua: bangun ruangan dari prefab ruangan 16×20 ubin, rangkai dalam graf, bukan ubin per ubin — jauh
lebih murah untuk performa mobile dan tetap terasa prosedural.

**Monetisasi.** Gratis + iklan opsional, IAP hero baru (32 hero = 32 produk kosmetik/karakter yang
sudah jadi), battle-pass musiman.

**MVP realistis 8–10 minggu.** 1 biome, 4 hero, 6 musuh, 2 bos, 5 pola ruangan, 1 sistem upgrade
desa, save antar-run.

**Risiko utama.** Ini satu-satunya konsep yang **butuh tuning rasa aksi di layar sentuh** —
menu-slide kontrol, hit-stop, invincibility frame, dan auto-aim. Perkirakan 2 minggu khusus untuk
"merasakan enak", dan uji di HP kelas bawah sejak hari pertama.

---

## 7. Perbandingan langsung

| | R1 Ubin Bayangan (JRPG) | R2 Ladang Malam (Farm + bertahan) | R3 Gua Vertikal (Roguelite) |
|---|---|---|---|
| Kompleksitas teknis | **Rendah** (giliran, tanpa fisika berat) | Sedang | **Tinggi** (tuning aksi) |
| Kekayaan portofolio aset yang terpakai | ~95% | **~100%** | ~100% |
| Kecepatan ke MVP | **5–6 minggu** | 6–8 minggu | 8–10 minggu |
| Risiko "terasa enak di layar sentuh" | Sangat rendah | Rendah | **Tinggi** |
| Ukuran pasar Play Store | Sedang | **Besar & tahan lama** | Besar, kompetitif |
| Potensi retensi harian | Sedang | Tinggi | **Tinggi** |
| Beban penulisan konten | **Tinggi** (dialog, stat) | Sedang | Rendah |
| Keunikan vs. kompetitor | Monster-tamer bercita rasa segar | Farm + pertahanan malam masih langka | Pasar padat |
| Paling butuh pembelian tambahan | Musik + UI | Musik + UI | Musik + UI + SFX aksi |

## 8. Saran urutan eksekusi

1. **Mulai dari R1 (Ubin Bayangan).** Cakupan aset hampir penuh, risiko teknis paling kecil, dan
   layout pertarungan portrait-nya sudah terbukti di pasar. Semua sistem yang dibangun untuk R1
   (tilemap portrait, UI TMP, save JSON, data monster di ScriptableObject) **langsung dapat
   dipakai ulang** untuk R2 dan R3.
2. **Siapkan pondasi bersama lebih dulu (1–2 minggu), lalu pilih arah:**
   project baru Unity 6000.3.20f1 (Universal 2D) + junction `Assets/Gif` dari master, Pixel Perfect
   Camera 16 PPU, Input System sentuh, UI TMP 9-slice dari `color_palette.png`, serta editor tool
   kecil untuk menjelajah 57 battler & 32 ARPG dan menandainya ke data game.
3. **R2 adalah taruhan jangka panjang** kalau tujuannya retensi/iklan berhadiah; simpan sebagai rilis
   kedua karena dunia dan ekonomi desanya bisa memakai kembali seluruh hub R1.
4. **R3 hanya dikejar kalau** R1/R2 sudah terbukti dan ada waktu khusus untuk tuning kontrol.

## 9. Yang tidak bisa datang dari master dan harus dipenuhi sendiri

| Kebutuhan | Status | Saran |
|---|---|---|
| Musik & SFX | **Tidak ada sama sekali** | Beli **Cozy RPG Music Bundle** (penerbit sama, nadanya pasti serasi) atau lisensi dari penyedia musik game |
| Sprite UI/HUD, tombol, panel | **Tidak ada** (hanya 2 sheet ikon tani 16×16) | Buat 9-slice dari `Res/color_palette.png` paket; pakai ikon tani sebagai acuan ukuran |
| Font | **Tidak ada** | Pakai font pixel berlisensi komersial untuk TMP |
| Ikon aplikasi & grafik Play Store | Tidak ada | Buat dari hero/battler + background paket (boleh, karena untuk game Anda sendiri) |
| Analytics, IAP/iklan, cloud save | Tidak ada | Google Play Games Services + Firebase (atau SDK pilihan Anda) |
| Data game (stat, quest, ekonomi) | Tidak ada | ScriptableObject / CSV; jangan hardcode |

**Pengingat izin pakai (dari `AGENTS.md` master).** Aset ini berbayar dan hanya boleh dipakai untuk
game milik pemilik akun Asset Store-nya: jangan di-commit ke repo publik, jangan dibagikan file
mentahnya, dan **jangan** dipakai untuk melatih atau menyuapi model AI. Sebelum rilis, pindahkan dari
Cara A (junction) ke **Cara B** (salin hanya yang terpakai, keluarkan dari `Resources`) supaya ukuran
APK/AAB wajar. Jangan pernah membuat game di dalam folder master.

---

# Bagian II — Tiga Konsep Cerita (default English, sederhana tapi epik)

## 10. Bahasa default: English

Kabar baik: hasil pemindaian master menemukan **nol file font dan nol teks yang menempel di sprite**
(yang ada hanya 2 sheet ikon tani 16×16 berisi gambar benda, bukan huruf). Artinya tidak ada satu pun
aset yang perlu diedit untuk membuat game berbahasa Inggris sejak awal.

Cara paling ringkas: semua string masuk ke **satu tabel JSON** (kunci → teks) dengan `en` sebagai
default, lalu TMP membacanya. Paket `com.unity.localization` belum ada di manifest dan untuk tiga
cerita ini belum diperlukan — kalau nanti mau menambah bahasa lain, cukup tambah kolom `id` di file
yang sama tanpa mengubah kode.

Contoh tabel:

```json
{
  "menu.play":        { "en": "Play" },
  "menu.continue":    { "en": "Continue" },
  "menu.language":    { "en": "Language" },
  "hud.day":          { "en": "Day {0}" },
  "hud.gold":         { "en": "Gold" },
  "battle.attack":    { "en": "Attack" },
  "battle.capture":   { "en": "Befriend" },
  "farm.harvest":     { "en": "Harvest" },
  "toast.night_watch": { "en": "Night is falling. Hold the field." }
}
```

Aturan penulisan teks untuk layar portrait: **maksimal 3 baris per layar**, satu kalimat per baris,
12–16 karakter per kata. Portrait sempit — teks panjang langsung terasa seperti tembok.

## 11. Prinsip "sederhana tapi epik" yang saya pakai di ketiga cerita

| Sederhana | Epik |
|---|---|
| Satu tujuan yang bisa diucapkan dalam satu kalimat | Taruhannya menyangkut dunia, bukan satu desa |
| Satu tokoh utama, tanpa cabang cerita | Pemain **melihat** perubahan besar di layar, bukan membacanya |
| Tanpa dialog bercabang, tanpa 50 NPC berplot | Perubahan itu terjadi pada aset yang sama, cuma terlihat berbeda |
| Cerita lewat komposisi statis + 2–3 baris teks | Terasa besar karena dipendam lama, lalu dilepas sekali |

Karena master tidak punya aset cutscene, cerita disampaikan dengan 3 cara yang tidak butuh aset baru:

1. **Komposisi statis** — sprite paket dipajang di atas salah satu dari 11 `Backgrounds/`, plus
   2–3 baris teks, plus fade. Cukup untuk seluruh cerita.
2. **Perubahan keadaan dunia** — layar judul, langit, tint palet, dan bangunan desa berubah sesuai
   progres. Ini yang membuat epik terasa, dan biayanya hampir nol.
3. **Buku harian / surat** — pakai `Animations/Books` (7 frame) dan `Cristal` (27 frame). Satu
   kalimat per entri. Pemain yang mau tahu akan membacanya; yang mau cepat bisa lewat.

---

## 12. S1 — "The Moon Thief" (pasangan konsep R1: JRPG turn-based portrait)

> **The moon was stolen. Go get it back.**

**Premise.** One night the moon simply does not rise. The sky stays black, crops stop growing, and
things that used to hunt only in the dark now walk in broad daylight. Mira, a farm kid with a rusty
shovel and a habit of not minding her own business, finds the only clue in the village: a set of
footprints the size of a cart, walking away from the sky and down the Sunken Road. She packs four
loaves of bread, borrows her three friends, and leaves before sunrise.

**Kenapa epik.** Langit rusak **sepanjang game** — pemain melihat langit kosong di setiap layar
tempur, karena 11 `Backgrounds/` itu semuanya berlatar luar/gua. Epiknya diendapkan puluhan jam,
lalu dilepas sebagai satu perubahan layar di ending.

**Cast.** Mira = `Hero/hero/color_1` (punya walk, run, attack, bow, throw, lift, carry, shield_walk,
shield_block, spin, hit, death). Tiga teman = tiga karakter `ARPG` (pilih tipe senjata berbeda:
pedang, tombak, tongkat — sheet-nya sudah ada). 57 Sealed Beast = `Battlers/`. Moon Thief =
`MinotaurA` atau `BlackMagusA` diperbesar sebagai bos akhir.

**Lima ketukan cerita.**

| # | Ketukan | Isi di layar | Aset yang dipakai |
|---|---|---|---|
| 1 | **The Long Night** | Desa gelap, warga menutup pintu, langit kosong | `Houses`, `chara_0..31`, `Lamp` 18 |
| 2 | **Four Roads** | Empat babak: Plain → Forest → Desert → Sunken Road; tiap babak ditutup satu Sealed Beast yang memberi satu petunjuk | `Backgrounds` PlainA/B, ForestA–C, DesertA/B, DungeonA–D; `TilePalette` |
| 3 | **The Beast That Ate the Sky** | Bos tengah; petunjuk terakhir: yang dicuri bukan bulan, tapi tempat bulan digantung | `Monsters` 43 sheet, `Battlers` |
| 4 | **Climb the Giant Tree** | Pohon yang menembus langit jadi tangga terakhir | `gigantic_pack` (2.7.0), `stacked_trees` |
| 5 | **Put It Back** | Langit menyala, layar judul ikut berubah, peta dunia yang sama kini berpendar bulan | Ulang `TilePalette` dengan tint + pencahayaan berbeda — **tanpa aset baru** |

**Kail yang bikin fun.** Setiap Sealed Beast yang kalah bisa **ditangkap**, dan alasannya bukan
karena pemain kuat: beast itu ikut kehilangan sesuatu yang besar di langit dan mau ikut mencari. Satu
kalimat penutup per tangkapan, misalnya: *"The wasp folds its wings. It misses the light too."*
Ini menjelaskan mekanik collection tanpa perlu lore rumit, dan langsung memakai `Battlers` A–H
sebagai tier evolusi (SlimeA → SlimeH).

**Loop harian.** Pagi garap kebun di desa (bahan ramuan) → ekspedisi satu babak → dungeon ubin,
monster terlihat berjalan (43 `Monsters`) → pertarungan turn-based → tangkap → pulang.

**Copy Play Store (English).**
- Judul: **The Moon Thief**
- Short description (60/80): *The moon has been stolen. Raise four heroes and take it back.*
- Baris pertama deskripsi: *A short, bright turn-based RPG about a kid, three friends, and one very
  large thief. Befriend 57 beasts. Bring back the sky.*

---

## 13. S2 — "Grandpa's World Tree" (pasangan konsep R2: cozy farm + pertahanan malam)

> **One seed. One field. One tree that holds up the sky.**

**Premise.** Grandpa left you two things: a field of dead soil, and a single seed in a jam jar.
Everything else was sold to pay debts. The note taped to the jar says, *"It only grows if you never
let the dark take it."* So you plant it. By day you hoe, water, and haul stones. By night the forest
sends things to eat it.

**Kenapa epik (dan kenapa ini pemetaan aset terbersih dari semuanya).** Pertumbuhan pohon itu
**adalah** kurva progresi game — 7 tahap, dan delapan aset tahap tumbuh sudah tersedia sebagai sprite
di `Animations/Farm/farm_plant_00..20`. Tahap akhir memakai `gigantic_pack`: pohon yang menembus awan.
Tidak ada satu pun aset baru yang perlu digambar untuk adegan paling megah di game.

**Lima ketukan cerita.**

| # | Ketukan | Isi di layar | Aset yang dipakai |
|---|---|---|---|
| 1 | **The Seed in the Jar** | Lahan kosong, satu lubang, satu benih | `Crops/Sprites`, `farm_hoe`, `farm_shovel` |
| 2 | **First Sprout, First Raid** | Kecambah muncul; malam pertama ada yang datang mencabutnya | `farm_plant_*`, `Monsters` 43, `Trap` 3, `Fire` 3 |
| 3 | **They Come Back** | Karena ada yang tumbuh, warga desa kembali satu per satu — 32 wajah berbeda | `chara_0..31` + `CharacterAppearance.cs` (ganti spritesheet saat runtime) |
| 4 | **Above the Clouds** | Batang pohon menembus awan; pemain bisa memanjat | `gigantic_pack`, `stacked_trees` |
| 5 | **What Grandpa Cut Down** | Akar menahan langit yang hampir jatuh. Ternyata kakek pernah menumbuhkan satu pohon seperti ini, menebangnya untuk menyelamatkan desa, dan tidak pernah bercerita | `Statues` 3, `Torii` 6, `Rock` 46, `Cristal` 27 |

**Kail yang bikin fun.** Pemain bukan pahlawan berpedang; dia anak dengan ember dan pagar kayu. Yang
paling seru bukan pertarungannya, tapi menyadari pukul 03.00 pagi bahwa **pagar yang kamu bangun tadi
siang** itulah yang menyelamatkan pohon. Semua pertahanan dibangun dari 247 prefab yang sudah ada
(Crate 8, Barrel 5, Rock 46, Columns 10, Torch 17, Trap 3).

**Tanpa villain.** Kegelapan tidak butuh tokoh jahat. Yang dibutuhkan cuma gelombang malam (`Monsters`
+ `Battlers` sebagai elite) — cerita tetap epik tanpa perlu satu pun dialog tokoh jahat.

**Copy Play Store (English).**
- Judul: **Grandpa's World Tree**
- Short description (51/80): *Grow one seed into the tree that holds up the sky.*
- Baris pertama deskripsi: *Farm by day. Defend by night. Seven nights a year, the forest comes for
your tree — and every morning it is a little taller than the clouds.*

---

## 14. S3 — "One Meter Down" (pasangan konsep R3: roguelite turun gua portrait)

> **The village sinks one meter every day. Dig down and find out why.**

**Premise.** Every sunrise the village is a little lower than it was yesterday. Fences lean. The
well's rope needs one more knot. Nobody talks about it. You take a lantern, tie the rope around your
waist, and climb down the well — into an abandoned mine that nobody in the village remembers digging.

**Kenapa epik.** Kalau konsep R1 membuat langit rusak, di sini **tanah** yang gagal. Semakin dalam
kamu turun, semakin tua reruntuhannya: tambang paling atas masih rapi, dan lapisan paling bawah sudah
jadi batu. Ini alasan teknis yang indah untuk memakai **7.424 tile `Legacytiles` versi lama** sebagai
strata tertua, sementara `Tiles` baru dipakai di lapisan atas.

**Lima ketukan cerita.**

| # | Ketukan | Isi di layar | Aset yang dipakai |
|---|---|---|---|
| 1 | **The Well** | Tali, lentera, dan pagar desa yang miring | `Prefabs/Crates`, `Barrels`, `Lamp` 18, `Torch` 17 |
| 2 | **Floors 1–10: The Old Miners** | Jejak penambang yang sudah lama hilang; kereta tambang masih berjalan | `Kart` 5 (kereta = naik-turun cepat antar lantai), `Switch` 3, `Door` 18, `Cristal` 27 (titik simpan) |
| 3 | **The Journals** | Buku-buku catatan: *"Day 411. It moved again. We should have stopped."* | `Animations/Books` 7 + 1 kalimat per entri — seluruh cerita, tanpa satu adegan pun |
| 4 | **Older Than Stone** | Lapisan bawah memakai tile warisan; Guardian per lantai | `Legacytiles` 7.424, `Battlers` sebagai Guardian, `Lava` 5, `Trap` 3 |
| 5 | **It Was Never a Monster** | Yang ada di dasar bukan monster, tapi sesuatu yang **bernafas** dan menarik desa ke bawah. Bukan dibunuh: dikembalikan apa yang dulu dicuri penambang | `Cristal` 27 (jantungnya), `gigantic_pack` (akar raksasa) |

**Kail yang bikin fun.** Meta-progresi terasa langsung di badan: setiap run yang selesai **mengangkat
desa satu meter**. Pemain bisa naik ke permukaan dan melihat desanya lebih tinggi, pagarnya lebih
lurus, tetangganya lebih tenang. Cerita pahit yang sederhana, tapi dorongan untuk turun lagi jadi
sangat kuat.

**Copy Play Store (English).**
- Judul: **One Meter Down**
- Short description (55/80): *The village sinks a meter a day. Dig down and stop it.*
- Baris pertama deskripsi: *A one-thumb descent into an abandoned mine. Every run pushes the village
back up a meter — and every journal below remembers why it started sinking.*

---

## 15. Perbandingan ketiga cerita

| | S1 The Moon Thief | S2 Grandpa's World Tree | S3 One Meter Down |
|---|---|---|---|
| Pasangan konsep gameplay | R1 (JRPG turn-based) | R2 (farm + pertahanan malam) | R3 (roguelite turun gua) |
| Tujuan dalam 1 kalimat | Ambil bulan kembali | Tumbuhkan benih jadi pohon langit | Cari tahu kenapa desa tenggelam |
| Cara cerita disampaikan | Komposisi statis + tangkapan beast | Pertumbuhan pohon + warga yang kembali | Buku harian + strata tambang |
| Aset yang dipakai sebagai "aktor" cerita | `Battlers` 57, `Backgrounds` 11 | `farm_plant_*`, `gigantic_pack`, `chara_0..31` | `Books` 7, `Legacytiles` 7.424, `Cristal` 27 |
| Villain | Ada satu (Moon Thief) | **Tidak ada** — hanya malam | Ada, tapi bukan monster (sesuatu yang bernafas) |
| Adegan "megah" termurah | Langit/peta yang sama dengan tint bulan | Pohon raksasa di atas desa | Akar raksasa di dasar |
| Beban penulisan teks | Sedang (±150 baris) | **Ringan** (±80 baris) | **Ringan** (±60 baris, gaya buku harian) |
| Ending yang mudah dibuat ulang | Moonlit Run (New Game+) | Pohon tahap lebih tinggi | Deep Run |

**Pilihan saya:** **S2 dan S3** adalah dua cerita yang paling "sederhana tapi epik" per jam kerja —
keduanya bisa diceritakan dengan kurang dari 100 baris teks dan tetap terasa besar, karena besarnya
datang dari **perubahan keadaan dunia** yang dilihat pemain, bukan dari panjangnya dialog. **S1**
paling kaya dan paling menjual (58 makhluk yang bisa ditangkap = 58 kail emosional), tapi butuh
penulisan lebih banyak, jadi paling pas kalau R1 yang dipilih sebagai game pertama.
