# 0010 — chore(agents) Faz 3–4: `/gate`, `/mutate`, `/commit`, `/pr` ve iş akışı belgeleri (ara kayıt)

## Kapsam

Bu bir ara (interim) öğrenme kaydı. `chore/agents-workflow-skills` dalını, `cbe608e4e15652d9554042920a2955beb23d559c` HEAD'inde, temiz bir çalışma ağacıyla anlatıyor.

- **0010 bir günlük sıra numarası.** GitHub PR numarası ya da roadmap numarası değil. İnsan, planın "Learning journal (decision 11)" adımının ilk maddesini kaydetti: `ls docs/journal` on girdi listeledi, `ls docs/journal/0010-* 2>/dev/null | wc -l` de `0` bastı.
- **Hâlâ bekleyenler.** Faz 4 kapanmadı. Bu PR ne açıldı ne tamamlandı. P4.4 de başlamadı: tüm dalın code review'ı, `/security-review`, PR-end tester, `/gate` ve `/pr` sırayla bekliyor. Bu kaydın commit'i bekliyor. İnsanın explainer çağrılarını hook log'uyla eşleştirmesi (journal adımı 3) ve son kanıt commit'i (final evidence commit) de bekliyor. `/security-review` bu dalda hiç çalışmadı. H0.1 ve H4.1 yalnızca komutun otomatik tamamlamada göründüğünü kaydeden insan gözlemleri.
- **Kayıt türlerini ayrı tutuyorum.** 0009'daki ayrım burada da geçerli:
  - **Mekanik kayıt:** shell'in ya da araç çalışma zamanının yakaladığı komut, çıktı ve `exit=` satırı.
  - **Model raporu:** bir ajanın ya da ana oturumun yazdığı metin. Coder, reviewer, planner ve tester handback'leri ve reviewer verdict dosyaları da buna dahil. Bunlar incelenecek iddialardır, doğrulanmış sonuç değil. Bir `APPROVED` ya da `READY_TO_PUSH` kodu doğrulamaz; bir modelin kararını kaydeder.
  - **İnsan gözlemi** ve **insan kararı.**

### Faz 4 tester geçmişi

| Çalıştırma | Ne oldu | Kayıt türü |
|---|---|---|
| Run 1 (launch `toolu_01EuuYgaj3HXid49gEaQKCXa`, HEAD `cbe608e`) | Rapor `Verdict: READY_TO_PUSH` ve `Gates-sha256: 8466131a…` yazdı. | model raporu |
| | Aynı tester, salt okunur bir ek (supplement) isteğine verdiği ikinci handback'te bu kararı geri çekti: "READY_TO_PUSH does not hold as written. No: not for cbe608e…". Kendi saydığı eksikler: verdict dosyasındaki gate stdout'u verbatim değildi, X5–X11 çalıştırılmamıştı, adım 6 G2–G11'e eşlenmemişti ve manual trace bitmemişti. | model raporu |
| | Ek yanıt bir kez bir safety classifier tarafından durduruldu. Transcript'te `stop_reason=refusal` taşıyan bir kayıt ve bir bildirim satırı var. Nedeni bu kayıtlardan belirlenmiyor. | mekanik kayıt |
| | İnsan run 1'i eksik saydı ve kararını kullanmadı. Rapor değiştirilmeden `validation/tester-verdict-4-cbe608e-1.md`'ye taşındı; sha256 taşımadan önce ve sonra aynı (`299cba6a…`). | insan kararı, mekanik kayıt |
| Run 2 (launch `toolu_01VDyC2GdGoikgQyRmxzFoMk`, HEAD `cbe608e`) | Rapor `READY_TO_PUSH` ve `Gates-sha256: 0eb7ae09…` yazdı. | model raporu |
| | Ana oturum, prompt'un dört Faz 4 dosyasını adıyla verdiğini açıkladı (`CLAUDE.md`, `docs/roadmap.md`, `docs/ai-workflow.md`, `.claude/agents/explainer.md`). Probe protocol item 5 (F13) ise "prompt dosya listesi vermez" diyor. | model raporu |
| | Ana oturumun yazdığı ve sha256'sı eşleştikten sonra çalıştırılan korelasyon betiği `tester-2-match=1` ve `exit=1` döndürdü. Tek başarısız kontrol F13. Diğer kontroller geçti. Gate stdout karşılaştırması sondaki boşlukları yok sayıyor, yani bayt bayt bir karşılaştırma değil. | mekanik kayıt |
| | İnsan run 2'yi ve `cbe608e`'deki doğrulamayı, açıklanmış F13 prosedür sapmasıyla kabul etti. Kaydın kendisi: "Independent scope discovery without supplied file names was not established." Aynı kayda göre bu kabul O1'i ya da taşınan herhangi bir sınırlamayı kapatmıyor ve Faz 4'ü ya da P4.4'ü tamamlanmış saymıyor. | insan kararı |

İnsanın kendi doğrulaması iki log dosyasında. `validation-4-cbe608e-20261020T225557.log` X1–X12 ve D1–D10'u, `gate-checks-4-tester-cbe608e-20261020T225602.log` de run 2'nin kopyası `gates-4-tester-cbe608e-2.txt` üzerinde G0–G11'i tutuyor. İnsan kararına göre ikisi de beklenen sonuçla eşleşiyor; G10, önceden kabul edilen kalıp çakışmasıyla.

### Açık kalan ve taşınan sınırlamalar

- **O1:** `disable-model-invocation: true` bu ortamda kanıtlanmamış bir uygulama sınırı (residual item 22). Açık.
- **G10:** kabul edilmiş kalıp çakışması. Ayrıntısı "Dikkat edilen tuzaklar" bölümünde.
- **P3.4:** M1.1 denemesi geçiş değil. M1.1-r2 üç prosedür sapmasıyla kabul edildi:
  - bir ek salt okunur plan kontrolü yapıldı;
  - mutasyon Edit'i ile test çalıştırması birlikte gönderildi;
  - ters Edit ile temizlik de birlikte gönderildi.

  Katı sıralı yürütme kanıtlanmadı.
- **Hiç çalıştırılmamış dallar:** `gate.sh`'ın base-check hata yolu, üç betiğin `cd` hatası, ve kayıt ya da log başarıyla açıldıktan sonraki yazma, okuma ve son-satır hataları. Bunları yalnızca kod incelemesi kapsıyor.
- **N5/O4 ve DV15 kapsam boşluğu:** Şu yollar residual item (10)'da yok: `gate.sh`'ın `missing:` ve `sha256=unavailable` dalları, `mutate-run.sh`'ın dotnet modu, birden çok dosyayla çalışan `mutate-clean.sh`. DV15'in "her çalıştırılmamış dal item (10)'da adlandırılır" cümlesi `missing:` ve `sha256=unavailable` için tutmuyor. İnsan kararı: N5 taşınıyor, (10)'a eklenmiyor.
- **N7:** "Phase 4 human prerequisites" ve "R4 restart and H4.1" bölümlerinde her `exit=` satırından önce boş bir satır var, planın `run` fonksiyonu ise öyle yazmıyor. Kaydedicisi belli olmadığı için bu iki bölümün provenance'ı kurulmadı. Komutları, çıktıları ve çıkış kodları kaydedildiği gibi duruyor. R4'ün başlama saati de kaydedilmedi.
- **N8:** Faz 4 kod commit'i `77f07f8`'in gövdesi plan:1620'nin istediği cümleyi içermiyor ve ne "#15"e ne "this follow-up"a atıf yapıyor. Bu kaçırılmış bir gereklilik. Tarihçe yeniden yazılmadı. Eksik cümle, düzeltme commit'i `dc37158`'in gövdesinde ileriye doğru kaydedildi; bu kayıt `77f07f8`'i uyumlu yapmıyor.
- **Canlı `/pr` yolu** ilk kez P4.4'te kullanılacak.

## Ne yaptık

Bu dal, PR #15'in ertelediği Faz 3 ve Faz 4'ü kuruyor. Faz 3'te `/verify` silindi ve yerine `/gate` geldi. Onun betiği `gate.sh`, hook testleri, `dotnet format`, `dotnet build` ve `dotnet test` dahil on adımı çalıştırıyor. Her adımın ham çıktısını ve kendi çıkış kodunu `TestResults/<branch-slug>/gates.txt`'e yazıyor, özeti de modelin okumasından değil bu dosyadan hesaplıyor. Tester artık bu betiği bir kez çalıştırıyor, komutları yeniden koşmadan kaydı inceliyor ve dosya listesini kayıttan alıyor. Ayrıca:
- `/mutate` ve iki betiği, mutasyon kanıtlarını commit'lenmiş koda karşı standartlaştırıyor.
- `/commit` mesajı bir dosyadan `git commit -F` ile veriyor.
- `/pr` yalnızca HEAD'e bağlı verdict dosyalarını kabul eden yedi önkoşul koyuyor ve yalnızca doğrulanmış head'i birleştiriyor.

Faz 4'te `CLAUDE.md` planın Appendix A'sına (199 satır) eşitlendi, 762 satırlık `docs/ai-workflow.md` eklendi, `docs/roadmap.md` güncellendi ve explainer'ın günlük numarası PR numarasından ayrıldı. Ürün koduna dokunulmadı: run 2 kopyası üzerindeki insan log'unda `src/`, `tests/`, `db/` ya da `.claude/hooks/` altında değişen yol yok. Aynı log'a göre hook testleri 216, .NET testleri 638 (120 + 126 + 392). Önceden `/verify` komutları modele çalıştırtıp pipe'lı çıktılarını yapıştırtıyordu, `/pr` de yalnızca aynı oturumda dönmüş bir tester kararına güveniyordu.

## Yeni giren teknolojiler

### Betik destekli skill'ler (`SKILL.md` + `scripts/`)
- **Ne işe yarar:** Skill, sen `/ad` yazınca ana oturuma enjekte edilen bir talimat dosyası. Frontmatter'ı birkaç şeyi belirler:
  - `allowed-tools`: skill çalışırken izin istemeden kullanılabilecek araçlar.
  - `effort`: skill'in akıl yürütme düzeyi.
  - `disable-model-invocation`: skill'in kim tarafından başlatılabileceği.
  - `/ad`'dan sonra yazdığın metin skill'e `$ARGUMENTS` olarak gelir.

  Bu PR'daki yenilik şu: işi talimat metni değil, skill'in yanındaki bir betik yapıyor. Model yalnızca betiği çağırıp stdout'unu yapıştırıyor. Frontend'den bir benzetme: veri çekme mantığını bileşenin içine yazmak yerine test edilmiş bir servise taşımak gibi. Bileşen yalnızca servisin döndürdüğünü gösterir.
- **Bu projede nerede:**
  - Yeni: `.claude/skills/gate/SKILL.md` ile `.claude/skills/gate/scripts/gate.sh`; `.claude/skills/mutate/SKILL.md` ile `scripts/mutate-run.sh` ve `scripts/mutate-clean.sh`.
  - Yeniden yazılan: `.claude/skills/commit/SKILL.md` ve `.claude/skills/pr/SKILL.md`.
  - Silinen: `.claude/skills/verify/SKILL.md`.
  - `.claude/settings.json`'a eklenen üç allow girişi. X3'ün `diff` çıktısında yalnızca bunlar görünüyor.
- **Alternatifi neydi:** Üç seçenek vardı.
  - `/verify`'ı tutup talimatını sıkılaştırmak. Eski skill komutları sıralıyor, `dotnet build -warnaserror 2>&1 | tail -5` gibi pipe'larla çalıştırıyor ve modelin çıktıyı aktarmasına güveniyordu. Ayrıca adı Claude Code'la gelen bir skill'le çakışıyordu (F5).
  - Node betiği. Hook'lar Node'da yazılı.
  - Gate'i yalnızca CI'da çalıştırmak.

  Bash seçildi, çünkü `bash-allowlist.js:36` tester'a zaten tam olarak `bash .claude/skills/gate/scripts/gate.sh` komutuna izin veriyordu; R3-3 bu girişi "uykuda" diye kaydetmişti. Git Bash de projenin shell'i. Verdiklerimiz:
  - Shell için bir test çatısı yok. Onun yerine P3.7/P3.8'in atılabilir betiği var ve otomatik bir test bırakmıyor.
  - Hiçbir hook betiğin içinde neyin çalıştığını denetlemiyor. Betiği sınırlayan tek şey incelenmiş içeriği (residual item 2).
- **Nerede okunur:** https://code.claude.com/docs/en/skills

### Bash: dosya tanımlayıcıları, `exec`, `eval` ve `set -u`
- **Ne işe yarar:** Dosya tanımlayıcısı (file descriptor, fd), açık bir dosyaya verilen numaradır: 0 stdin, 1 stdout, 2 stderr. `exec 3>"$REC"` dosyayı bir kez açar, içini boşaltır (truncate) ve 3 numarasına bağlar. Sonraki her `>&3` yazması aynı açık dosyaya gider. `eval "$cmd"` bir string'i komut olarak çalıştırır. `set -u` tanımsız bir değişkenin kullanılmasını hata sayar.
- **Bu projede nerede:**
  - `gate.sh:19`: `set -u`.
  - `gate.sh:38`: `{ exec 3>"$REC"; } 2>/dev/null || fail 3 "I/O error: open $REC"`.
  - `gate.sh:89`: `eval "$cmd" </dev/null >&3 2>&3`.
  - Mutation betiklerinde log, ekleme (append) için açılıyor: `exec 3>>"$LOG"`.
- **Alternatifi neydi:** Her yazmada dosyayı yeniden açmak (`>> "$REC"`). Tek açılış, "bu kayda yazamıyorum" durumunu hiçbir adım çalışmadan önce tek bir yerde yakalıyor. Bunu P3.7'nin `d-open` durumu çalıştırdı: kayıt yoluna bir dizin konduğunda betik `rc=3` ile ve tam olarak `gate.sh: I/O error: open TestResults/scratch/gates.txt` satırıyla çıktı (mekanik kayıt). Verdiklerimiz:
  - Bash'in kendi hata mesajlarını bastırmak için her yazma `{ ...; } 2>/dev/null` ile sarılıyor, bu da okunabilirliği düşürüyor.
  - Descriptor'ın kapatılmasındaki bir hatayı bash'in bildirip bildirmediği DOĞRULANMAMIŞ (residual item 10).
- **Nerede okunur:** https://www.gnu.org/software/bash/manual/html_node/Redirections.html ve https://www.gnu.org/software/bash/manual/html_node/The-Set-Builtin.html

### GitHub CLI: `gh pr merge --match-head-commit` ve `--body-file`
- **Ne işe yarar:** `gh`, GitHub'ın komut satırı aracı.
  - `gh pr merge --match-head-commit <sha>`: PR'ın head commit'i bu SHA değilse GitHub birleştirmeyi reddeder.
  - `gh pr create --body-file <dosya>`: PR gövdesini bir dosyadan okur, böylece gövde shell tırnaklamasından hiç geçmez.
- **Bu projede nerede:** `.claude/skills/pr/SKILL.md`. Adım 1 HEAD'i V olarak kaydeder, adım 5 gövdeyi dosyadan verir, adım 8 de `gh pr merge --squash --delete-branch --match-head-commit V` çalıştırır. Bu dalda henüz hiç çalışmadı.
- **Alternatifi neydi:** Eski skill'in iki yolu: `gh pr create --body "<body>"` ve bayraksız `gh pr merge --squash --delete-branch`. Branch protection'ı atlayan `--admin` her durumda yasak. Verdiğimiz şey şu: doğrulanmış head ile PR'ın head'i ayrıştığında `/pr` durur ve süreç o kaydı üreten adımdan yeniden başlar. "Dalın son halini birleştir" diye bir kestirme yok.
- **Nerede okunur:** https://cli.github.com/manual/gh_pr_merge ve https://cli.github.com/manual/gh_pr_create

### `git commit -F` ve `git stripspace`
- **Ne işe yarar:** `git commit -F <dosya>` mesajı dosyadan okur. Mesaj komut satırına hiç girmediği için shell tırnakları onu bölemez. `git stripspace`, git'in bir mesajı commit'e koymadan önce yaptığı temizliği uygular: sondaki boş satırları atar, satır sonundaki boşlukları siler.
- **Bu projede nerede:**
  - `/commit`'in 4. ve 5. adımları.
  - `allowed-tools`'taki tek commit girişi `Bash(git commit -F TestResults/COMMIT_MSG.txt)`.
  - P3.5'in mesaj karşılaştırması (aşağıda, "Komutlar").
- **Alternatifi neydi:** `git commit -m "..."`. Araştırmanın F16 bulgusuna göre Windows PowerShell 5.1 bir mesajı çift tırnaklarından böldü. Model kendi push'lanmamış commit'ini `git reset --soft HEAD~1` ile geri alıp `-F` ile yeniden commit'leyerek kurtardı. Yeni skill bu kurtarmayı da yazılı bir kurala bağladı: soft reset yalnızca bu çağrının yaptığı, push'lanmamış bir commit için. Verdiklerimiz:
  - Her commit bir dosya yazımı gerektiriyor.
  - Write aracı okumadığı bir dosyanın üstüne yazmadığı için `Read` de `allowed-tools`'a girdi.
  - `/commit` artık `--amend` ya da başka bir commit biçimini izin istemeden çalıştıramıyor.
- **Nerede okunur:** https://git-scm.com/docs/git-commit ve https://git-scm.com/docs/git-stripspace

## Kavramlar

### Skill, ajan ve hook: üç ayrı genişletme noktası
Ajan (subagent) kendi bağlamında çalışan ayrı bir Claude örneği. Skill ana oturumun içine enjekte edilen bir talimat. Hook ise her araç çağrısından önce çalışan bir komut. `/gate` bir skill olduğu için ana oturumda çalışır. Tester ise aynı betiği kendi bağlamında, kendi `bash-allowlist:tester` hook'unun altında çalıştırır. Ana oturumun yazdığı dosyalara (`COMMIT_MSG.txt`, `PR_BODY.md`, security-review dosyası) hiçbir write-scope hook'u uygulanmaz, çünkü write-scope yalnızca ajan frontmatter'ından kurulur. Onlara yalnızca global path-guard uygulanır.

### `allowed-tools` ve izinlerin daraltılması
Bir skill çalışırken `allowed-tools`'ta listelenen araçlar izin istemeden çalışır. Bu PR listeleri daralttı. Eski `/commit` `Bash(git commit:*)`, `Bash(git switch:*)` ve `Bash(git branch:*)` taşıyordu. Yenisi yalnızca `Bash(git commit -F TestResults/COMMIT_MSG.txt)`, `Bash(git switch -c *)` ve `Bash(git branch --show-current)` taşıyor. Listede olmayan bir komut yasak değildir, yalnızca izin istemi çıkarır. Asıl yasak `permissions.deny`'dadır.

### `disable-model-invocation: true` ve O1
Projenin bu bayrağa yüklediği anlam şu: skill yalnızca insan `/ad` yazınca çalışır. P3.4'ün M1.1 denemesinde insanın çok satırlı isteği slash komutu olarak değil, yapıştırılmış düz metin olarak geldi (insan gözlemi; transcript satırı 498). Bunun üzerine ana oturum Skill aracını kendisi çağırdı (satır 508) ve çağrı başarılı oldu (satır 509, "Launching skill: mutate"). Runtime'ın bu çağrıyı reddetmesi gerekip gerekmediği kurulmadı, yani bunun bir hata mı yoksa bayrağın amaçlanan anlamı mı olduğu bilinmiyor. Aynı davranış `/commit` ve `/pr` için hiç denenmedi. Bu yüzden bayrak bu ortamda kanıtlanmamış bir uygulama sınırı ve O1 açık.

### Gate kaydı: başlık, bölümler, `required` ve `info`
`gates.txt`'in ilk satırı başlıktır: zaman, dal, `head=`, `main=`, `origin_main=` ve iki merge-base. Her adım bir bölüm yazar: `$ <komut>` satırı, adımın ham stdout ve stderr'i, sonra `exit=<n> step=<ad> kind=<required|info>`. Son satır `gate-exit=<0|1> failed=<liste>`'dir. Onu `final_status` yazar: kayıttaki `kind=required` satırlarını geri okur ve altı zorunlu adımın her biri için tam olarak bir satır, o satırda da `exit=0` ister. `info` adımları (`status`, `files`, `log`, `docker`) kaydedilir ama `gate-exit`'i belirlemez. Çalışma ağacının temiz olduğunu `status` bölümünün boş olması gösterir.

### Çıkış kodu sözleşmesi: 0, 1, 2, 3
`gate.sh` dört kodla çıkar:
- 0: kayıt tam, doğrulanmış ve son satırı `gate-exit=0 failed=`.
- 1: kayıt tam ve doğrulanmış, ama zorunlu bir adım başarısız.
- 2: başlangıç hatası; hiçbir şey yazılmadı.
- 3: kayda yazma ya da okuma hatası.

2 ve 3'e giden her yol stderr'e tam olarak bir satır yazar: `gate.sh: <sebep>`. Bu, 0009'daki "kapalı başarısızlık" fikrinin betiğe taşınmış hali: kaydını yazamayan ya da doğrulayamayan bir gate asla geçmiş gibi görünmemeli. P3.2 bunu bilerek bozulmuş bir testle sınadı. Mekanik kayıt: `ℹ pass 215`, `ℹ fail 1`, `gate-exit=1 failed=node` ve `gate=1`. İnsan kararı: geçti.

### Bayat kayıt ve "kayıt, hash'ini basan çalıştırmanındır"
Her `gate.sh` çalıştırması `gates.txt`'in üstüne yazar, ama başlangıçta (exit 2) duran bir çalıştırma hiçbir şey yazmaz ve eski kaydı yerinde bırakır. Bu yüzden insan, kaydı kullanılacak her çalıştırmadan önce canlı kaydı siler (`absent=0`). Bir kayıt da ancak stdout'u tam olan, yani o kaydın yolunu, sha256 satırını ve son `gate-exit=` satırını basan çalıştırmaya ait sayılır. Tester'ın `Gates-sha256:` satırı bu bağı kaydeder. Run 2'de rapordaki hash ile insanın kopyasının hash'i aynı: `0eb7ae09…` (mekanik kayıt).

### Kanıt kopyaları ve en küçük boş `n`
Kayıt silinmeden önce `validation/gates-<label>-<HEAD7>-<n>.txt` adıyla kopyalanır. `n`, hedefi henüz var olmayan en küçük pozitif sayıdır. Böylece aynı HEAD'deki ikinci çalıştırma yeni bir `n` alır ve hiçbir kopyanın üstüne yazılmaz. Faz 4'te run 1'in kopyası `gates-4-tester-cbe608e-1.txt`, run 2'ninki `gates-4-tester-cbe608e-2.txt`. Tester raporları da aynı biçimde taşınıyor (`tester-verdict-4-cbe608e-1.md`), çünkü tester var olan bir raporun üstüne yazmaz; dosya varsa durup bunu bildirir.

### Git'in üç durumu: HEAD, index ve çalışma ağacı
HEAD son commit'tir. Index (staging area), `git add` ile hazırladığın bir sonraki commit'in taslağıdır. Çalışma ağacı da diskteki dosyalardır. Bir form üzerinden düşünebilirsin: kaydedilmiş hali (HEAD), "taslak olarak kaydet" dediğin hali (index) ve hâlâ yazmakta olduğun hali (çalışma ağacı).

Üç karşılaştırma üç farklı soru sorar:
- `git diff --exit-code HEAD --`: çalışma ağacı HEAD'e eşit mi?
- `git diff --cached --exit-code --`: index HEAD'e eşit mi?
- düz `git diff --exit-code`: çalışma ağacı index'e eşit mi?

P3.8'in i durumunda dosya değiştirilip `git add` ile index'e alınıyor. Index ile çalışma ağacı aynı içeriği taşıdığı için düz diff hiçbir fark görmez, ama ikisi de HEAD'den farklı. `mutate-clean.sh`'ın iki karşılaştırmayı birden yapmasının nedeni bu.

### Commit'lenmiş koda karşı mutasyon kanıtı
Sıra şöyle:
1. Dosyanın HEAD'e eşit olduğu kanıtlanır (`mutate-clean.sh` exit 0).
2. Dosya Edit ile bozulur.
3. Test çalışır.
4. Ters Edit ile geri alınır.
5. Temizlik yeniden kanıtlanır.

Geri alma `git checkout --` ile değil ters Edit ile yapılır, çünkü `git checkout --` deny listesinde. Bir kırmızı ancak testin kendi assertion'ı patladıysa sayılır. M1.1-r2'nin log'unda 32 `AssertionError` satırı var; ayrı bir `ENOENT` hatası assertion kanıtı sayılmadı (insan kararı).

### Etiket doğrulaması ve yol kaçışı
Mutasyon etiketi bir dosya adına dönüşür (`mutations/<label>.txt`), bu yüzden `^[A-Za-z0-9][A-Za-z0-9._-]*$` ile sınırlı. `../x` gibi bir etiket `mutations/` dizininin dışına yazardı. P3.8'in e2 ve f1 durumları tam bu etiketi verdi ve iki betikte de usage satırı ile exit 2 kaydedildi. Bu, 0009'daki write-scope `..` sorununun shell'deki karşılığı.

### Verdict dosyası, kind, `-rN` ve Head kuralı R
Her reviewer kararını `thoughts/shared/reviews/YYYY-MM-DD_<branch-slug>-<kind>[-rN].md` adlı bir dosyaya yazar. Dosyanın ilk beş satırı `Verdict:`, `Reviewer:`, `Scope:`, `Range:` ve `Head:`. Bir kind içinde yalnızca en yüksek N sayılır.

Head kuralı R iki şart koyar: `Head:` gerçek bir commit olmalı, ve ya HEAD'e eşit olmalı ya da ondan HEAD'e kadarki değişikliklerin hepsi `thoughts/shared/reviews/` altında olmalı. Bu dalda canlı bir örnek var. phase4-r2 incelemesinin `Head:`'i `2d698c6`, HEAD ise `cbe608e`. Bu kaydı yazarken çalıştırdığım `git diff --name-only 2d698c6… cbe608e…` yalnızca `thoughts/shared/reviews/2026-10-20_chore-agents-workflow-skills-code-review-phase4-r2.md`'yi bastı.

### Light lane ve PR-end sırası
Küçük bir docs, fix ya da chore PR'ı (şema değişikliği ve yeni public davranış olmadan) researcher, planner ve plan-reviewer'ı atlayıp kısa bir plan dosyasıyla başlayabilir. PR sonu iki şeritte de aynı:
1. tüm dalın code review'ı
2. `/security-review`
3. inceleme dosyalarının commit'i
4. tester
5. `/gate`
6. `/pr`

Kanıt, belge, günlük ve ADR commit'leri 1. adımdan önce gelir. 4. adımdan sonraki her commit tester'ın kararını geçersiz kılar ve tester yeniden çalışır. Bu dalın P4.4'ü bu sıranın ilk gerçek kullanımı olacak.

### `/security-review` ve `Verdict: PENDING`
`/security-review`, Claude Code'un yerleşik bir komutu; projenin bir skill'i değil. Ana oturum çıktısını aynen bir dosyaya kaydeder ve başlığa `Verdict: PENDING` yazar. `SECURITY_CLEAR` ya da `SECURITY_FINDINGS`'i yalnızca insan yazar, çıktıyı okuduktan sonra. İnceleme yalnızca değişen her yol `docs/` ya da `thoughts/` altındaysa atlanır. Bu dal `.claude/` altını değiştirdiği için atlanamaz.

### Ek rapor (supplement)
Bir tester raporu eksik kalırsa insan aynı tester'dan salt okunur bir ek ister. Ek asıl raporu değiştirmez, yanına eklenir. Faz 3'te (P3.6) asıl rapor 8–10. adımlar (manual trace, adı geçen kontroller, ham çıktı) bakımından eksikti ve iki ek bu adımları tamamladı. İnsan P3.6'yı mekanik kayıtlara, onaylı code review'a ve aynı tester'ın asıl raporuyla iki ekine dayanarak kabul etti; aynı kayda göre bu, her artık dalın çalıştırıldığını göstermiyor. Faz 4 run 1'de ise ek, asıl raporun `READY_TO_PUSH`'unu geri çekti.

### Gerekli, ek ve INCONCLUSIVE durumlar; harness hatası
P3.7 ve P3.8'de gerekli (required) durumlar probun sonucuna karar verir. `chmod` ile yapılan ek (supplementary) durumlar bir probu asla geçirmez; yalnızca DEFECT çıkarsa ilgili probu düşürür. Atlanan (`-skipped`) gerekli bir durum INCONCLUSIVE sayılır ve INCONCLUSIVE asla PASS değildir. Kurulumun ya da test düzeneğinin kendi komutunun hatası (harness failure) test edilen betiğin kusuru sayılmaz: o çalıştırmada hiçbir prob geçmez, insan sorunu giderip yeniden çalıştırır. P3.7/P3.8 tek çalıştırmada `setup=0`, `== done` ve `removed=0` bastı ve insan iki probu geçti saydı.

### `GIT_CEILING_DIRECTORIES` ve çalışma ağacı koruması
Git bir komutu çalıştırmadan önce `.git` dizinini bulmak için üst dizinlere doğru arar. `GIT_CEILING_DIRECTORIES` bu aramanın geçemeyeceği bir tavan koyar. P3.7/P3.8, "çalışma ağacında değil" durumlarını (a, e8, f6) ancak bu tavanla git'in `S/notree`'de bir çalışma ağacı bulamadığını doğruladıktan sonra çalıştırdı (`notree-guard=128`). Bu koruma olmasaydı, test edilen gate geçici dizinin üstündeki ilgisiz bir repoda çalışabilirdi.

### Yeniden başlatma kuralı (R3, R4)
Claude Code'un skill, ajan ve ayar dosyalarını oturum ortasında ne zaman yeniden okuduğu DOĞRULANMAMIŞ (F21). Bu yüzden bu dosyaları değiştiren her commit'ten sonra Claude Code yeniden başlatılıyor ve değişikliğe ancak ondan sonra güveniliyor. Bunun iki somut sonucu oldu:
- Faz 3 kod commit'i `7241447` eski `/commit` tarifiyle yapıldı ve `COMMIT_MSG.txt` yazılmadı (S1 kaydı). Bu yüzden P3.5, yeniden başlatmadan sonraki ilk commit olarak tanımlandı.
- İlk yeniden başlatma denemesi eski oturumu sürdürdüğü için R3 sayılmadı.

### Düzeltme döngüsü ve append-only As built
Bir inceleme NEEDS_REVISION dönerse şu sıra izlenir: coder'ın düzeltme commit'i, yüklenen bir dosya değiştiyse yeniden başlatma, etkilenen problar, eklemeli bir As built kopyası ve `-r2` incelemesi. Kanıt dosyası plana yalnızca eklenerek kopyalanır ve kopya `cmp=0` ile doğrulanır. Faz 4'te bu döngü bir kez döndü:
- phase4 incelemesi R1 ve R2 ile NEEDS_REVISION verdi (model raporu, Head `a4a1eb0`).
- `dc37158` yalnızca `docs/ai-workflow.md`'yi değiştirdi.
- phase4-r2 APPROVED verdi (model raporu, Head `2d698c6`).

### `CLAUDE.md`'nin 200 satırlık bütçesi
`CLAUDE.md`'nin kendisi, dosya yaklaşık 200 satırı geçerse kuralların yok sayılmaya başladığını söylüyor. Plan bunu kanıtlanmış bir gerçek olarak değil, bir gelenek (convention) olarak okuyor. Faz 4'ün içeriği tam yazıldığında yaklaşık 205–215 satır tutuyordu; Appendix A ifadeleri sıkıştırarak 199'a indirdi. Verdiğimiz şey kural metinlerinin kısalığı: ayrıntılar `docs/ai-workflow.md`'ye taşındı ve `CLAUDE.md` yalnızca ona işaret ediyor.

### Regresyon kontrolü ve tarihsel kanıt
G0–G11, X1–X12 ve D1–D10, mevcut kod ve ayarın belirli bir HEAD'de hâlâ geçtiğini gösterir. Eski bir kanıtı yeniden kurmazlar. Faz 1–2'nin mutasyon kanıtları ve canlı probları tekrarlanmadı. P3.4, M1.1'in mutasyonunu yalnızca `/mutate`'i sınamak için yeniden çalıştırdı; Faz 1'in kanıtını yeniden değerlendirmedi.

## Komutlar ve ne yaptıkları

- `bash .claude/skills/gate/scripts/gate.sh`: Argüman almaz ve `git fetch` yapmaz. On adımı ve `final_status`'u çalıştırır. Bash aracının `timeout`'u 600000 ms (10 dakika) olarak verilir. P3.1'in kaydında tool_use `01:19:27.096Z`'de, tool_result `01:21:43.898Z`'de; çalıştırma yaklaşık 2 dakika 17 saniye sürdü ve varsayılan 2 dakikalık sınırı aşardı. Run 2'de aynı aralık `00:32:25.988Z` ile `00:34:10.413Z` arasında.
- `bash .claude/skills/mutate/scripts/mutate-clean.sh <label> <file>...`: İki diff kaydını `mutations/<label>.txt`'e ekler ve aynı satırları stdout'a da basar.
  - 0: dosyalar hem index'te hem çalışma ağacında HEAD'e eşit.
  - 1: bir diff fark buldu.
  - 2: argüman ya da başlangıç hatası, ya da git'in 0 ve 1 dışında bir kodla çıkması. P3.8'in q durumunda, HEAD'i doğmamış (unborn) bir repoda, git 128 döndü ve betik 2 ile çıktı.
- `bash .claude/skills/mutate/scripts/mutate-run.sh <label> <file> node <test-file>`: Önce `git diff HEAD -- <file>`'ı, yani mutasyonun kendisini, sonra testi kaydeder. Stdout'a log yolunu, assertion satırlarını ve testin `exit=` satırını basar. `dotnet "<filter>" [<project>]` modu bu dalda hiç çalışmadı (O4).
- `git ls-files --error-unmatch -- <file>`: Dosya git'te izlenmiyorsa hata verir. İki betik izlenmeyen bir hedefi bununla exit 2 ile reddediyor (P3.8 e5 ve f2).
- `git rev-parse --verify --quiet "<sha>^{commit}"`: `^{commit}` ifadeyi bir commit nesnesine çözer, `--verify` tam olarak tek bir geçerli nesne ister, `--quiet` hata mesajını bastırır. Head kuralı R'nin ilk adımı bu; yanlış yazılmış bir SHA burada düşer.
- `git merge-base --is-ancestor A B`: A, B'nin atasıysa 0, değilse 1 döner. Burada exit 1 bir hata değil, "hayır" cevabıdır (`/pr` precondition 7).
- `git log -1 --format=%B | diff - TestResults/COMMIT_MSG.txt; echo "diff=$?"`: P3.5'in bağlam kaydı. Çıktı `7d6`, `< ` ve `diff=1` oldu. Tek fark sondaki bir boş satır, çünkü `--format` bir "tformat"tır ve çıktının sonuna satır sonu ekler. Plan bunu önceden DV8 olarak yazmıştı.
- `git cat-file commit HEAD | sed '1,/^$/d' | diff - <(git stripspace < TestResults/COMMIT_MSG.txt); echo "msg-diff=$?"`: P3.5'in asıl karşılaştırması ve sonucu `msg-diff=0`.
  - `git cat-file commit HEAD` commit nesnesini ham olarak basar: tree, parent, author ve committer başlıkları, bir boş satır, sonra mesaj.
  - `sed '1,/^$/d'` ilk boş satıra kadar olan her şeyi siler.
  - `<(...)` (process substitution) bir komutun çıktısını dosya gibi okutur.
  - Bunun yanında `git log -1 --format=%B | grep -c '"quoted"'` `1` bastı: tırnaklı kelime commit'e bölünmeden girdi.
- `rm -f TestResults/chore-agents-workflow-skills/gates.txt; test ! -e TestResults/chore-agents-workflow-skills/gates.txt; echo "absent=$?"`: Canlı kaydı kaldırır. `rm -f` dosya yoksa hata vermez. `test ! -e` dosya yoksa 0 döner. `$?` son komutun çıkış kodudur. Beklenen çıktı `absent=0`.
- Kopya komutu (Probe protocol item 8): `while [ -e "$V/gates-$L-$H7-$n.txt" ]; do n=$((n+1)); done` boş ilk `n`'i bulur. `test ! -e "$D" && cp ... && sha256sum ...` ancak hedef yoksa kopyalar ve iki dosyanın hash'ini basar. `echo "copied=$? path=$D"` de sonucu ve seçilen yolu yazar.
- `sed 's/\r$//' <kanıt dosyası> | cmp - <(sed -n '<first>,<last>p' <plan>); echo "cmp=$?"`: As built kopyasının kontrolü. `sed 's/\r$//'` satır sonundaki CR'yi siler, `sed -n 'a,bp'` yalnızca a ile b arasındaki satırları basar, `cmp` iki girdiyi bayt bayt karşılaştırır. Eşitlikte `cmp=0` basılır.
- `wc -l CLAUDE.md` (D1): Satır sayısını basar. Validation-4 log'unda `199 CLAUDE.md`, `exit=0`.
- D2: Appendix A'yı `awk '/^<!-- APPENDIX-A-START -->$/{f=1;next} /^<!-- APPENDIX-A-END -->$/{f=0} f'` ile işaretler arasından çıkarır, `sed '1d;$d'` ile çit (fence) satırlarını atar ve `diff - CLAUDE.md` ile karşılaştırır. Çıktı boş, `exit=0`: dosya Appendix A'ya eşit.
- `grep -n -i -e purchasing -e SoapCore -e "Windows Service" CLAUDE.md` (D4): Eskiden "bu kelimeler yok" kontrolü olurdu. Şimdi beklenen sonuç tam olarak bir satır, `8:`: "Purchasing" kelimesi kapsam dışı cümlesinde geçiyor. Cümle kelimeyi büyük harfle yazdığı için kontrol `-i` ile büyük/küçük harfe duyarsız (DV9).
- `bash -n <betik>` (X12): Betiği çalıştırmadan yalnızca sözdizimini kontrol eder.
- İnsanın `run` fonksiyonu ve runner: 0009'dakiyle aynı. Her komut `bash -c "$1" < /dev/null` ile çalışır ve `exit=<kod>` satırı log'a eklenir. Log adı `<K>-<HEAD7>-<UTC zaman>.log`; ilk satırı `HEAD <sha>`, ikinci satırı komut dosyasının sha256'sı. Validation-4 log'unda X3, X7, X11 ve D7 `exit=1` ile bitiyor ve dördü de beklenen sonuç. Örneğin X3'ün `diff`'i tam olarak üç eklenmiş allow satırını basıyor. "exit 1 = başarısızlık" kuralı burada da geçerli değil.
- `/gate`'in skill çağrısı ve C3.2: İnsan, transcript'te çağrı ile sonuç arasındaki satırlarda `"effort"` ve `"perTurnEffort"` değerlerini saydı. `/gate` için "observed: low" kaydedildi; `/commit` için 1059–1127 satırlarında 16 `"effort":"low"` ve 11 `"perTurnEffort":"low"` bulundu. Bu bir geçti/kaldı kontrolü değil, adı konmuş bir gözlem.
- **Bu kaydı yazarken benim çalıştırdıklarım.** Bash aracım sayısal çıkış kodu göstermedi. Çağrıları hook log'uyla eşleştirmek insanın işi (journal adımı 3); burada öyle bir iddiada bulunmuyorum.
  - `ls C:/projects/envanex/docs/journal`
  - `git -C C:/projects/envanex diff main...HEAD --stat` ve `git -C C:/projects/envanex log --oneline main..HEAD`. İkisinin de araç sonucu `Blocked by bash-allowlist:explainer: command is not in the explainer allowlist` oldu. Token bazında eşlemenin canlı bir örneği: komutun ilk iki token'ı `git -C`, izin girişi ise `git diff`. Ayrıca `git -C …` deny listesinde de var.
  - `git diff main...HEAD --stat` ve `git log --oneline main..HEAD`
  - `ls -la` ile `TestResults/chore-agents-workflow-skills` altındaki üç dizin
  - `git diff main...HEAD -- CLAUDE.md`
  - `git diff main...HEAD -- .claude/agents/coder.md .claude/settings.json .claude/skills/commit/SKILL.md`
  - `git diff main...HEAD -- .claude/skills/pr/SKILL.md .claude/skills/verify/SKILL.md`
  - `git status --short --branch`, `git diff --name-only 2d698c6… cbe608e…` ve `git log -1 --format=%H`

## Dikkat edilen tuzaklar

1. **Pipe çıkış kodunu yutar.** Eski `/verify` `dotnet build -warnaserror 2>&1 | tail -5` ve `dotnet test 2>&1 | grep -E "Passed!|Failed!|error|Error"` çalıştırıyordu. Bir pipe'ın çıkış kodu son komutunkidir, yani build başarısız olsa da `tail` 0 döner. `gate.sh` her adımı pipe'sız çalıştırıyor ve adımın kendi kodunu `exit=` satırına yazıyor. Tester'ın allowlist'i hâlâ `grep`, `head`, `tail` ve `wc`'ye pipe'a izin veriyor (residual item 11). Plan bunu, hiçbir karar tester'ın doğrudan çalıştırdığı bir `dotnet` komutuna dayanmadığı sürece kabul ediyor.
2. **Bayat kayıt.** Faz 3 incelemesi sırasında canlı `gates.txt` hâlâ P3.2'nin bilerek başarısız edilmiş kaydıydı: `head=7241447…` ve `gate-exit=1 failed=node` (inceleme gözlemi O6, model raporu). İnsan P3.6'dan önce bu kaydın korunmuş kopyayla bayt bayt aynı olduğunu `cmp` ile doğruladı, sonra sildi (`absent=0`, mekanik kayıt). Silme atlansaydı ve tester'ın gate çalıştırması başlangıçta (exit 2) dursaydı, P3.2'nin kaydı tester'ınkiymiş gibi yerinde kalırdı. Tester'ın 3. adımı bu yüzden stdout'u tam olmayan bir çalıştırmadan sonra `gates.txt`'i hiç okumuyor.
3. **Düz `git diff --exit-code` index'e alınmış bir değişikliği kaçırır.** Yukarıdaki i durumu. `mutate-clean.sh` bu yüzden HEAD'e karşı iki karşılaştırma yapıyor.
4. **Tırnaklı commit mesajları.** F16'daki bölünme, mesajın komut satırından geçmesinden doğuyordu. Yeni `/commit` mesajı dosyadan veriyor. P3.5 bunu gövdesinde çift tırnaklı `"quoted"` kelimesi olan bir commit'le sınadı. Faz 3 kod commit'i ise eski tarifle yapıldığı için mesajına hiç `"` konmadı; `grep -c -F "\""` `0` bastı.
5. **`--format=%B` sona bir satır sonu ekler.** Bir commit mesajını `git log` çıktısıyla bayt bayt karşılaştırırsan tek bir boş satır yüzünden `diff=1` görürsün. Doğru karşılaştırma, ham commit nesnesi ile `git stripspace` çıktısı arasında.
6. **Yeniden başlatmadan güvenmek.** Yüklenmemiş bir skill dosyasına güvenen bir prob, eski tarifi sınamış olur. Kurallar (R3, R4 ve "yüklenmemiş dosyaya güvenilmez") bu yüzden var. Faz 3 incelemesinin O2 gözlemine göre coder'ın yeniden başlatmadan önceki `gate.sh` çağrısı `"reasonType":"rule"` taşıyordu. Bu, yeni allow girişinin oturum ortasında okunmuş olabileceğini düşündürüyor ama hangi kuralın eşleştiği DOĞRULANMAMIŞ.
7. **Canlı bir guard'ı mutasyona uğratmak.** P3.4'ün hedefi `.claude/hooks/lib/guard-common.js:45`, yani path-guard'ın kullandığı `toSlashForm`. Ters Edit yapılana kadar bu mutasyon her araç çağrısını etkiler. Ters Edit'i guard'ın kendisi bloklarsa, insan dosyayı Claude Code dışında düzeltir ve deneme o adımda başarısız sayılır. İki denemede de ters Edit geçti.
8. **Test düzeneğinin gerçek repoya dokunması.** P3.7/P3.8 betiği yalnızca kendi yarattığı geçici dizinde çalışıyor. O dizin önceden varsa hiçbir şeye dokunmadan duruyor; temizlikte de yalnızca bu çağrının yarattığı dizini siliyor (`removed=0`), değilse `removed=skipped` yazıyor. "Çalışma ağacında değil" durumları `GIT_CEILING_DIRECTORIES` korumasının arkasında.
9. **dotnet'in CRLF çıktısı.** Faz 3 incelemesinin O5 gözlemine göre (model raporu) `gates-p3.1-7241447-1.txt`'nin 39 satırı CR ile bitiyor, çünkü dotnet CRLF yazıyor. Böyle bir satırı `$` ile sabitlenmiş bir grep bulamaz. G9'un kalıbı bu yüzden `[[:space:]]*$` ile bitiyor. `gate.sh`'ın kendi satırları (başlık, `$`, `exit=`, son satır) yalnızca LF.
10. **G10'daki kalıp çakışması.** G10, `grep -E 'Passed!|Failed!' "$GC"` ile tam olarak üç .NET satırı bekliyor. Mekanik kayıt dört satır gösteriyor; ilki bir hook testinin adı: `✔ bash-allowlist: blocks grep -E "Passed!|Failed!" (documented quote-unaware split)`. Bu satır bir .NET sonucu değil, `node` adımının çıktısından geliyor. İnsan bunu P3.1'de notla kabul etti, sonra da taşıdı. Kontrol düzeltilmedi.
11. **Model raporu kanıt değildir, kontrol betiği de yanılabilir.** İki örnek:
    - Faz 3'te tester'ın asıl raporu adım 8–10 bakımından eksikti ve iki ek gerektirdi. Faz 4'te run 1'in raporu `READY_TO_PUSH` dedi, aynı tester'ın eki onu geri çekti.
    - Faz 4 coder handback'inin ilk korelasyonu (r1) `handback-match=1` verdi. İnsan kaydına göre bunun nedeni teslimatın eksikliği değildi; betik `message.content`'in mesajı girintisiz tuttuğunu varsaymıştı. r1 kaydı silinmedi, yanına r2 eklendi (`handback-match=0`).
12. **Kaçırılmış bir gereklilik geriye dönük düzeltilmez.** N8: `77f07f8`'in gövdesi plan:1620'nin istediği atıfları içermiyor. Commit `--amend` ile değiştirilmedi ve force-push yapılmadı; ikisi de tarihçeyi yeniden yazmak olurdu ve force-push deny listesinde. Eksik cümle `dc37158`'in gövdesine eklendi. Bu bir ileriye doğru düzeltme; `77f07f8`'i uyumlu yapmıyor.
13. **Bir belge, kapsadığından fazlasını iddia edebilir.** DV15, çalıştırılmamış her dalın item (10)'da adlandırıldığını söylüyor. N5/O4 yolları orada yok ve item (10)'un "What only review covers" listesi tam bir liste gibi okunuyor. Boşluk şimdilik yalnızca planın kanıt bölümünde ve PR gövdesinin taşınan bulgularında duracak.

## Farklı düşündüğüm yer

- **Tester'ın effort düzeyi ile ondan istenen iş uyuşmuyor.** `docs/ai-workflow.md` tester'ı "bir betik çalıştırıp çıktısını yapıştırır" diye tanımlıyor ve düşük effort'u buna dayandırıyor. Ama `tester.md`'nin 8. adımı değişen dosyaların manual trace'ini, 9. adımı da adı geçen testlerin gerçekten bir şey assert ettiğinin kontrolünü istiyor. Bunlar mekanik değil, yargı gerektiren işler. Kayıtlar bunun bedelini gösteriyor:
  - P3.6'nın asıl raporu bu adımlarda eksik kaldı ve iki ek gerektirdi.
  - Run 1, kendi ekine göre (model raporu) 762 satırlık `docs/ai-workflow.md`'nin yalnızca ilk 140 satırını okumuştu, ve kararı geri çekildi.
  - Run 2'nin korelasyon betiği, tester'ın Read çağrılarının plandaki Appendix A aralığına ulaşmadığını kaydetti (mekanik gözlem). CLAUDE.md'nin Appendix A'ya eşitliğini zaten insanın D2'si mekanik olarak kuruyor.

  Ben tester'ı gerçekten mekanik tutardım: gate, G/X/D kontrolleri ve ham çıktı. Manual trace'i zaten `max` effort'la çalışan code-reviewer'a bırakırdım. Ya da belge ağırlıklı fazlarda tester'ı daha yüksek bir effort'la çalıştırırdım.
- **F13 kuralı hâlâ yalnızca bir metin.** Probe protocol item 5 "tester'ın prompt'u dosya listesi vermez" diyor ve `tester.md` de prompt'taki listeyi yok saymasını söylüyor. Yine de run 2'nin prompt'unu ana oturum dört dosya adıyla yazdı. Hiçbir şey bunu çalıştırmadan önce engellemedi; ana oturum sonradan açıkladı ve korelasyon betiği FAIL olarak kaydetti. Faz 2 bu tür kuralları prompt'tan hook'a taşıyarak çözmüştü. Ben tester'ın launch prompt'unu yalnızca plan yolunu ve aşamayı içeren, commit'lenmiş sabit bir şablon yapardım ve çalıştırmadan önce kontrol ederdim. Bedeli şimdi ödendi: bütün bir çalıştırmanın bağımsız kapsam keşfi kurulamadı ve bu bir sapma olarak kabul edildi.
- **N5/O4 yollarını okuyucuya açık olan belgeye yazardım.** İnsanın kararı onları item (10)'a eklemeden taşımak. Bu boşluk planın kanıt bölümünde ve PR gövdesinde duracak, ama `docs/ai-workflow.md`'yi okuyan biri item (10)'u eksiksiz bir liste sanacak. Eklemek tek bir cümle olurdu. Belgenin kendi tanımına göre UNVERIFIED olan bir şeyin okuyucunun gördüğü yerde adı konmalı.
- **Doğrulama düzeneği bu dalda da işin kendisinden ağır.** Dalın farkı 7.591 eklenen satır. Bunun yaklaşık 4.800'ü plan, yaklaşık 1.130'u sekiz inceleme dosyası. Betikler ve skill'ler yaklaşık 750, belgeler yaklaşık 870 satır. Git'e girmeyen `phase-4.md` kanıt dosyası tek başına yaklaşık 205 KB. Plan 2026-10-13'te başladı, bugün 2026-10-21 ve P4.4 hâlâ başlamadı; roadmap'te sırada bekleyen PR 7 ilk deploy. Kazandığımız şey somut: run 1'in `READY_TO_PUSH`'u ek rapor istenince tutmadı ve kullanılmadı, run 2'nin sapması da kayda geçti. Ama 0009'daki eleştiri burada da geçerli. Faz 3–4'ün betikleri bundan sonra kanıt toplamayı ucuzlatmalı; ucuzlatmıyorsa bir sonraki chore PR'ı light lane'de kalmalı.

## Kendini sına

1. P3.8'in j durumu şu sırayla çalışıyor: `f.txt` HEAD'de `a`; önce `printf 'b\n' > f.txt && git add f.txt`, sonra `printf 'a\n' > f.txt`. Bu anda `git diff --exit-code HEAD -- f.txt` ve `git diff --cached --exit-code -- f.txt` ayrı ayrı kaç döner, `mutate-clean.sh` hangi kodla çıkar? Düz `git diff --exit-code -- f.txt` ne derdi? Bu durum, i'deki kaçırmanın aynısı mı, tersi mi?
2. `gate.sh`'taki `fix_newline` son baytı `last=$(tail -c 1 -- "$REC" && printf x)` ile okuyup `$'\nx'` ile karşılaştırıyor. `printf x` olmadan `last=$(tail -c 1 -- "$REC")` yazıp `$'\n'` ile karşılaştırsaydık, çıktısı satır sonuyla biten bir adımdan sonra `gates.txt`'te ne değişirdi? G0–G11'den hangisi ya da hangileri bu değişiklikle beklenenden farklı sonuç verirdi?
3. `gate.sh` `set -u` ile başlıyor ama plan "never `set -e`" diyor. `set -e` açık olsaydı, `run_step`'teki `eval "$cmd"` sıfırdan farklı döndüğünde betik ne yapardı? P3.2'nin kaydı (bozulmuş bir hook testi) bu durumda nasıl görünürdü? Betiğin stdout'u, çıkış kodu ve tester'ın 3. adımı ne olurdu? Ortaya çıkan çıkış kodu, betiğin başındaki çıkış kodu sözleşmesiyle çelişir miydi?
4. `/pr` 7. adımda `gh pr view --json headRefOid --jq .headRefOid`'nin V bastığını kontrol ediyor, 8. adımda ise ayrıca `--match-head-commit V` veriyor. Biri yeterli olmaz mıydı? Yalnızca 7. adımdaki kontrol kalsaydı, hangi zaman aralığında ne olabilirdi? Yalnızca bayrak kalsaydı, 7. adımın `gh pr checks` yarısıyla birlikte ne kaybedilirdi?
5. `gate.sh:206`'daki komut `git diff --name-only main..HEAD` (iki nokta) olarak değiştirilse ve başka hiçbir satır değişmeseydi, şunlar nasıl etkilenirdi: (a) `gate.sh`'ın kendi özetindeki `files:` bloğu, (b) tester'ın dosya listesi (`tester.md` adım 4), (c) G5 ve G6, (d) `/pr` precondition 6? Bunlardan hangisi gürültülü bir hataya, hangisi sessiz bir "geçti"ye dönüşürdü? Bu bağımlılıkları bulmak için hangi dosyalarda neyi grep'lerdin?
6. `final_status` her zorunlu adım için yalnızca `exit=0` satırının varlığını değil, o adımın satır sayısını da (`n -ne 1`) kontrol ediyor. Kayıtta bir adımın `kind=required` satırı hangi koşulda sıfır kez, hangi koşulda iki kez bulunabilir? `final_status`'un grep'i dosyanın hangi kısımlarını tarıyor? G10'da gördüğün kalıp çakışması ile bu soru arasında nasıl bir ortaklık var?
7. Faz 4 run 1'in raporu (`Verdict: READY_TO_PUSH`, `Head: cbe608e…`) `tester-verdict.md` olarak yerinde kalsaydı ve P4.4'ün inceleme dosyaları HEAD `cbe608e` iken hazır olsaydı, `/pr`'ın 4. önkoşulu bu dosya için ne derdi? Aynı tester'ın geri çekmesini `/pr` görebilir mi; neden? Retention adımı (Probe protocol item 6) dışında bu açığı ne kapatabilirdi ve bunun bedeli ne olurdu?
8. Run 2'nin P3.6 kayıtlarında `list-diff=0` var (komut plan:1538'de). Bu kontrol neden F13 ihlalini yakalayamazdı? F13'ü yakalayan korelasyon kontrolü `rows-p4-tester-2.js`'te neye bakıyor? Dosya adı vermeden tester'ın kapsamını yine de yönlendiren bir prompt düşün; o kontrol onu yakalar mıydı?
