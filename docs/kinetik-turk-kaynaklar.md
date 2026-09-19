# Türk yazarların dissolüsyon kinetiği literatürü — rehber için kaynak taraması

Tarama tarihi: 2026-09-05. Amaç: Kinetik rehberinin kaynakçasına eklenecek Türk yazarlı çalışmaları
ve Türkiye'de yerleşmiş kinetik değerlendirme geleneğini belgelemek. Kaynaklar PubMed, Crossref,
Europe PMC, Semantic Scholar, DergiPark ve TEB e-kütüphane üzerinden doğrulandı; DOI verilen her
kayıt doğrulanmıştır. DOI'siz kayıtlar (eski Türkçe dergiler, tezler) künye düzeyindedir.

## 1. Üç ekol, tek gelenek

Türkiye'de dissolüsyon verisinin kinetik değerlendirmesi üç merkezde şekillenmiştir ve üçü de aynı
model setini kullanır: **sıfır derece, birinci derece, Hixson–Crowell (küp kök), "modifiye"
Hixson–Crowell, RRSBW (Weibull), Higuchi (karekök, Q√t), Hopfenberg, Langenbucher, modifiye
Langenbucher ve (Bt)^a**. Bu set, sitenin eski motorundaki (Kinetics.pas / Module1.bas) liste ile
birebir aynıdır; yani eski motor bu geleneğin doğrudan mirasıdır.

| Ekol | Kişiler | Katkı |
|---|---|---|
| Ankara (AÜ, sonra Gazi) | Enver İzgü, **İlbeyi T. Ağabeyoğlu**, Tamer Baykara, Nilüfer Yüksel | Kinetiğin kuramsal temeli; (Bt)^a kinetiğinin türetilmesi (1978); profil karşılaştırma yöntemleri (2000) |
| İzmir (Ege Ü.) | Gökhan Ertan, H. Yeşim Karasulu, Ercüment Karasulu, Mine Özyazıcı, Tamer Güneri, **Mehmet Ali Ege** | Model setinin IVIVC'de sistematik kullanımı; geometriye bağlı salım modelleri (Hopfenberg/Katzhendler) |
| İstanbul (İÜ) | **Ahmet Araman**, Yıldız Özsoy, Erdal Cevher, Sevgi Güngör, Mine Orlu | Uygulamalı formülasyon çalışmaları; salım verisinin "kinetik değerlendirmesi" |
| Hacettepe | A. Atilla Hıncal, H. Süheyla Kaş, Levent Öner | RRSBW kinetiği ve faktöriyel tasarım; "Farmakokinetik" kitabı (1986) |

## 2. İlbeyi Ağabeyoğlu

### Kinetiğe doğrudan katkısı: (Bt)^a kinetiği
Ağabeyoğlu, Modern Farmasötik Teknoloji'deki "Biyofarmasötik" bölümünde (2007, s. 430–447) kendi
kinetiğinin doğuşunu anlatır: sürekli akış hücresiyle yaptığı sülfametizol çalışmalarında
Langenbucher'in doğrusallaştırmasına göre çizilen grafik doğru değil parabol vermiş; bunun üzerine
t'nin üssünün 1'den farklı olabileceği varsayımıyla

    ln(1 − m/m∞) türünden doğrusallaştırma → y = a·ln t + a·ln b

biçiminde **(Bt)^a kinetiğini** türetmiştir (kaynak: doçentlik tezi, 1978). Bu, güç yasasının
(Korsmeyer–Peppas, 1983) Türkiye'de bağımsız ve daha erken bir kullanımıdır; Ege ekolü onu "(Bt)a"
adıyla model setine almıştır (Karasulu ve ark., 2003). **Yeni motorda bu model, Korsmeyer–Peppas
ile aynı eğri olduğu için ayrı listelenmez; rehberde bu eşdeğerlik ve tarihsel öncelik belirtilmeli.**

Aynı bölümde ayrıca: Noyes–Whitney'in özgün denklemi ve literatürdeki yanlış atıf, Hixson–Crowell
küp kök yasasının türetimi, Wagner kuramı (1969; yüzeyin üstel küçülmesi → asimptotlu birinci
derece, gecikme süresi t0), Kitazawa kuramı (iki aşamalı doğru, kesişim = dağılma anı), El-Yazigi
(dağılma-çözünme), Higuchi karekök kinetiği (homojen ve granüler matris), Langenbucher–Weibull
doğrusallaştırması ve f1/f2 benzerlik etkenleri örnekli olarak anlatılır. Rehberin "modellerin
tarihçesi" ve "Weibull/Wagner" kısımları için Türkçe birincil kaynak budur.

### Künyeler
- Ağabeyoğlu İ. **Biyofarmasötik.** İçinde: Modern Farmasötik Teknoloji (Acartürk F, Ağabeyoğlu İ,
  Çelebi N, Değim T, Değim Z, Doğanay T, Takka S, Tırnaksız F). TEB Eczacılık Akademisi, Ankara,
  2007; Bölüm 22. https://e-kutuphane.teb.org.tr/pdf/tebakademi/modern_farmasotk/24.pdf
- Ağabeyoğlu İ. **Temel Farmakokinetik.** Aynı kitap, Bölüm 13.
  https://e-kutuphane.teb.org.tr/pdf/tebakademi/modern_farmasotk/13.pdf
- Takka S, Acartürk F, Ağabeyoğlu İ, Çelebi N, Değim T, Değim Z. **Önformülasyon** (çözünürlük ve
  çözünme hızı tayini). Aynı kitap, Bölüm 11.
- Ağabeyoğlu İ. *Sürekli Etkili Sülfametizol Preparatının Biyofarmasötik Açıdan Tasarımı ve
  Gerçekleştirilmesi.* Doçentlik tezi, Farmasötik Teknoloji Kürsüsü, Ankara Üniv. Ecz. Fak., Eylül 1978.
  ((Bt)^a kinetiğinin kaynağı.)
- Ağabeyoğlu İT. Studies on Sustained Release I: The biopharmaceutical design and production of an
  inert matrix type sulfamethizole tablet, employing polymethylmethacrylate. *Drug Dev Ind Pharm*
  1985;11:2021–2041. https://doi.org/10.3109/03639048509087766
- Tarımcı NM, Ağabeyoğlu İT. Studies on Sustained Release III: Matrix granules of sulfamethizole.
  *Drug Dev Ind Pharm* 1985;11:2043–2056. https://doi.org/10.3109/03639048509087767
- Ağabeyoğlu İT. Studies on Sustained Release II: In vivo performance of the inert matrix
  sulfamethizole tablet. *Drug Dev Ind Pharm* 1986;12:423–430. https://doi.org/10.3109/03639048609026622
- Ağabeyoğlu İT. Studies on Sustained Release IV: Inert matrix tablets of sulfamethizole, employing
  polyvinyl chloride and carboxypolymethylene. *Drug Dev Ind Pharm* 1986;12:569–576.
  https://doi.org/10.3109/03639048609048029
- Studies on Sustained Release XI: Lipid granules of sulfamethizole. *Drug Dev Ind Pharm* 1990.
  https://doi.org/10.3109/03639049009025788
- İzgü E, Ağabeyoğlu İ. Mathematical investigation of in vitro diffusion rate of a drug from ointment
  bases. *Ankara Üniv. Ecz. Fak. Derg.* 1974;4(1). https://dergipark.org.tr/en/pub/jfpanu/issue/35582
- Baykara T, Doğanay T, Ağabeyoğlu İ, Ertan G. A study on soluble tablet formulations. *Ankara Üniv.
  Ecz. Fak. Derg.* 1980. https://dergipark.org.tr/en/pub/jfpanu/issue/35570/394921
- Teksin ZŞ, Ağabeyoğlu İ. Bioavailability of pentoxifylline–chitosan oral matrix tablet in healthy
  subjects. *J Bioequiv Availab* 2009. https://doi.org/10.4172/jbb.1000018
- Yamaç K, … Ağabeyoğlu İT, ... Comparison of the hematological effects of a sustained release
  chitosan formulation of pentoxifylline with a commercial formulation. *Arzneim-Forsch/Drug Res* 2000.
  https://doi.org/10.1055/s-0031-1300296
- Ocak F, Ağabeyoğlu İ. Development of a membrane-controlled transdermal therapeutic system containing
  isosorbide dinitrate. *Int J Pharm* 1999. https://doi.org/10.1016/S0378-5173(99)00005-8
- Değim Z, Ağabeyoğlu İ. Nonisothermal stability tests of famotidine and nizatidine. *Farmaco* 2002.
  https://doi.org/10.1016/S0014-827X(02)01266-1
- (1993) Influence of the preparation method on the dissolution characteristics of ibuprofen with
  low-molecular gelatin — künye Semantic Scholar'da, DOI bulunamadı.

Not: Ağabeyoğlu'nun PubMed'deki 9 kaydı ağırlıkla farmakokinetik/protein bağlanması (Wagner
laboratuvarı, 1979–1981) ve biyoeşdeğerlik çalışmalarıdır; dissolüsyon kinetiği katkısı Türkçe
kitap/tez ve DDIP serisindedir.

## 3. Ahmet Araman (İstanbul Üniversitesi)

Araman'ın yayınları formülasyon odaklıdır; özetlerde salım verisi "kinetik olarak değerlendirildi"
denir, model adı verilmez. Rehber için kinetik metodoloji kaynağı değil, **uygulama örneği**
niteliğindedir. Doğrulanan, salım/dissolüsyon içeren kayıtlar:

- Güngör S, Yıldız A, Özsoy Y, Cevher E, Araman A. Investigations on mefenamic acid sustained release
  tablets with water-insoluble gel. *Farmaco* 2003. https://doi.org/10.1016/S0014-827X(03)00040-5
- Güngör S, Orlu M, Özsoy Y, Araman A. In vitro studies on sustained release suppository formulations.
  *Sci Pharm* 2003. https://doi.org/10.3797/scipharm.aut-03-29
- Saygh MS, Uzunkaya G, Özsoy Y, Araman A. Enhanced dissolution rate of tiaprofenic acid using
  Gelucire 44/14. *Sci Pharm* 2002. https://doi.org/10.3797/scipharm.aut-02-28
- Orlu M, Cevher E, Araman A. Design and evaluation of colon specific drug delivery system containing
  flurbiprofen microsponges. *Int J Pharm* 2006. https://doi.org/10.1016/j.ijpharm.2006.03.025
- Yıldız A, Okyar A, Baktır G, Araman A, Özsoy Y. Nasal administration of heparin-loaded microspheres
  based on poly(lactic acid). *Farmaco* 2005. https://doi.org/10.1016/j.farmac.2005.08.004
- Yıldız A, John E, Özsoy Y, Araman A, ve ark. Inhaled extended-release microparticles of heparin.
  *J Control Release* 2012. https://doi.org/10.1016/j.jconrel.2012.07.008
- Araman A, Levi M. Preparation of the enteric coated tenoxicam tablets. *Eur J Pharm Sci* 1994;2(1–2).
  https://doi.org/10.1016/0928-0987(94)90461-8
- Araman A, Çaybaşı P, Güven KC. Stability of meclofenoxate hydrochloride in artificial gastric and
  intestinal media. *Pharmazie* 1992.
- Cevher E, Şensoy D, Taha MA, Araman A. Effect of thiolated polymers to textural and mucoadhesive
  properties of vaginal gel formulations. *AAPS PharmSciTech* 2008. https://doi.org/10.1208/s12249-008-9132-y

## 4. Ege ekolü — model setinin en açık yazılı hâli

Rehberdeki "Türkiye'de yaygın model seti" cümlesi için birincil kaynaklar:

- Karasulu E, Aktoğu S, Karasulu HY, Aydoğdu A, Tuğlular I, Ertan G. Improving of the accuracy of
  in vitro–in vivo linear correlation using kinetic models for ultra sustained release theophylline
  tablets. *Eur J Drug Metab Pharmacokinet* 2003;28:301–307. https://doi.org/10.1007/BF03220183
  — Özette açıkça: "zero-order, first-order, RRSBW, Hixson-Crowell, Higuchi, Hopfenberg,
  Langenbucher, modified Langenbucher and (Bt)a kinetic models"; ölçüt determinasyon katsayısı.
- Ertan G, Karasulu HY, Karasulu E, **Ege MA**, Köse T, Güneri T. A new in vitro/in vivo kinetic
  correlation method for nitrofurantoin matrix tablet formulations. *Drug Dev Ind Pharm* 2000.
  https://doi.org/10.1081/DDC-100101292 — modifiye Langenbucher (Weibull + Tlag) ve "ters kinetik" yöntem.
- Ertan G, Karasulu E, Özgüney I, Karasulu Y, Apaydın Ş, Kantarcı G, Yurdasiper A, **Ege MA**.
  Acceleration of in vitro dissolution studies of sustained release dosage form of theophylline and
  in vitro–in vivo evaluations in terms of correlations. *Eur J Drug Metab Pharmacokinet* 2011;36:243–248.
  https://doi.org/10.1007/s13318-011-0049-6
- Karasulu HY, Ertan G, Köse T. Modeling of theophylline release from different geometrical erodible
  tablets. *Eur J Pharm Biopharm* 2000. https://doi.org/10.1016/S0939-6411(99)00082-X — Hopfenberg
  ve Katzhendler denklemleri; üçgen için n=4, yarım küre için n=1.5 önerisi (rehberin Hopfenberg
  geometri bölümü için önemli).
- Karasulu HY, Ertan G. Different geometric shaped hydrogel theophylline tablets: statistical approach
  for estimating drug release. *Farmaco* 2002. https://doi.org/10.1016/S0014-827X(02)01297-1
- Özyazıcı M, Gökçe EH, Ertan G. Release and diffusional modeling of metronidazole lipid matrices.
  *Eur J Pharm Biopharm* 2006. https://doi.org/10.1016/j.ejpb.2006.02.005 — güç yasası (Fick/relaksasyon).
- Karasulu E, Karasulu E, Karasulu HY, Ertan G, Kırılmaz L, Güneri T. Extended release lipophilic
  indomethacin microspheres. *Eur J Pharm Sci* 2003. https://doi.org/10.1016/S0928-0987(03)00048-4
- Karasulu HY, Ertan G, Köse T, Güneri T. In vivo/in vitro correlations of nitrofurantoin matrix
  tablet formulations. *Eur J Drug Metab Pharmacokinet* 1996. https://doi.org/10.1007/BF03190275
- Ertan G, Özer Ö, Baloğlu E, Güneri T. Sustained-release microcapsules of nitrofurantoin and
  amoxicillin; preparation, in-vitro release rate, kinetic and micromeritic studies.
  *J Microencapsul* 1997. https://doi.org/10.3109/02652049709051140
- Ertan G, Sarıgüllü İ, ve ark. Sustained-release dosage form of nitrofurantoin. Part 1. Preparation of
  microcapsules and in vitro release kinetics. *J Microencapsul* 1994. https://doi.org/10.3109/02652049409040443
- Gökçe EH, Özyazıcı M, Ertan G. The effect of geometric shape on the release properties of
  metronidazole from lipid matrix tablets. *J Biomed Nanotechnol* 2009. https://doi.org/10.1166/jbn.2009.1052
- Özyazıcı M. *Nikardipin hidroklorür mikrokapsülleri üzerine çalışmalar.* Doktora tezi, Ege Üniv., 1994
  — "(Bt)a, sıfır derece, birinci derece, Hixson-Crowell, RRSBW, Higuchi, Q/√t ve Hopfenberg" seti.
  https://acikerisim.ege.edu.tr/items/fe52befc-1d8f-42fd-b922-efcc1f9495f3
- Ege MA, Üstündağ Okur N, Karasulu HY, Güneri T. Development and in vitro evaluation of theophylline
  loaded matrix tablets prepared with direct compression. *Indian J Pharm Educ Res* 2016.
  https://doi.org/10.5530/ijper.50.2.17

## 5. Ankara — profil karşılaştırma ve piyasa çalışmaları

- Yüksel N, Kanık AE, Baykara T. Comparison of in vitro dissolution profiles by ANOVA-based,
  model-dependent and -independent methods. *Int J Pharm* 2000;209:57–67.
  https://doi.org/10.1016/S0378-5173(00)00554-8 — rehberde zaten var; f1/f2 ve model-bağımlı
  karşılaştırmanın ayırt ediciliği.
- Kaynar Özdemir N, Duman G, Özateş B, Betendeniz B, Karataş A, Ermiş D, Yüksel A. Türkiye ilaç
  piyasasında bulunan değişik aspirin tablet formülasyonları üzerinde araştırmalar. *Ankara Üniv.
  Ecz. Fak. Derg.* 1989;19(1). https://dergipark.org.tr/tr/pub/jfpanu/article/394742 — en iyi uyum:
  modifiye Hixson–Crowell, RRSBW, birinci derece.
- Baloğlu E, Hızarcıoğlu Y. Quality control studies on enalapril maleate tablets available on the
  Turkish drug market. *Ankara Üniv. Ecz. Fak. Derg.* 2001. https://dergipark.org.tr/en/pub/jfpanu/article/394494
  — "zero, first order, Hixson Crowell, Modified Hixson Crowell, RRSBW, Q, Higuchi, Hopfenberg";
  en iyi: modifiye Hixson–Crowell ve RRSBW.

## 6. Hacettepe

- Öner L. *Çinko sülfat preparatlarının formülasyonu ve biyoyararlanımı üzerinde çalışmalar.* Doktora
  tezi (danışman H.S. Kaş), Hacettepe Üniv., 1987 — RRSBW kinetiği (Langenbucher 1976) + faktöriyel
  tasarım; T63.2 ve determinasyon katsayısı ile değerlendirme.
- Hıncal AA, Kaş HS, Hıncal F. *Farmakokinetik.* 1. baskı, Emekli Ofset, Ankara, 1986.

## 7. Rehber için çıkarımlar

1. **(Bt)^a = güç yasası.** Ağabeyoğlu'nun 1978 türetimi Korsmeyer–Peppas'tan (1983) öncedir; motor
   KP'yi kullanır, rehber eşdeğerliği ve önceliği söyler.
2. **RRSBW / Langenbucher / modifiye Langenbucher = Weibull (+Tlag).** Türk literatüründe üç ad,
   motorda tek model + varyant. Rehberde sözlük tablosu gerekir.
3. **"Modifiye Hixson–Crowell" ve "Q√t"** Türk çalışmalarında sık; rehberde Hixson–Crowell (+Tlag) ve
   Higuchi (+F0) karşılıkları verilmeli.
4. **Model seçimi ölçütü**: Türk literatüründe neredeyse yalnız determinasyon katsayısı (r²)
   kullanılmıştır. Rehber, r²'nin parametre sayısını cezalandırmadığını ve AIC/MSC'nin neden tercih
   edildiğini bu bağlamda açıklamalı (Costa & Sousa Lobo 2001; Zhang ve ark. 2010).
5. **Hopfenberg geometri**: Karasulu–Ertan–Köse 2000, geometri üsteli için 1/2/3 dışında değerler
   (üçgen 4, yarım küre 1.5) önerir; rehberde "n geometri sabitidir" cümlesine dipnot.
6. Türk yazarlarda **f1/f2** kullanımı: Ağabeyoğlu 2007 (örnek hesap), Yüksel 2000 — F1F2 sayfasının
   rehberine de aktarılabilir.
