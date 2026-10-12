# 0009 — chore(agents) Faz 2: rol başına hook'lar, komut izin listeleri ve yazma kapsamları (ön kayıt)

## Kapsam

Bu bir ön (preliminary) öğrenme kaydı. `chore/agents-hardening` dalında Faz 2'nin uygulanmış halini ve `ca46009dd2617a829f32df446f65e3d4a40e1a71` HEAD'inde yapılan ön tester çalıştırmasını (R9.9) anlatıyor.

- **0009 bir günlük sıra numarası.** GitHub PR numarası da değil, roadmap numarası da değil. İnsanın 2026-10-10 tarihli D3 kararı (planın Revision 9'u) bu numarayı bu kayıt için ayırdı. Hardening PR'ı ne açıldı ne tamamlandı.
- **Hâlâ bekleyenler:** Faz 2'nin son kod incelemesi (R9.12), insanın son Validation çalıştırması ve READY_TO_PUSH kararı, kapsam defteri (coverage ledger) ile As built kopyası (R9.11), Faz 3 ve Faz 4, ve hardening PR'ının tamamlanması.
- **Dayandığım tester raporu:** tamamlanmış ve saklanmış rapor, `TestResults/chore-agents-hardening/validation/tester-verdict-prelim-ca46009-2.md`. İlk, yarım kalan `-1` raporu değil. Bu rapordaki `READY_TO_PUSH` bir model raporu ve yalnızca bu kaydın tetikleyicisi (R9.10) olarak kullanıldı. Bu kaydın yazılmasına izin vermek ise insanın ayrı bir kararı. İkisi de Faz 2'nin READY_TO_PUSH'u değil: Faz 2'yi kapatmıyor, push'a izin vermiyor.
- **Raporun sınırları:** (1) Saklanan Bash kayıtlarında sayısal çıkış kodu yok. "Beklenen exit 0/1" sonuçları `is_error` ve `interrupted` alanlarına, grep için de `returnCodeInterpretation` alanına ve çıktı koşullarına dayanıyor. (2) Talimata rağmen bazı komutlar üst üste bindi: komut 4 ile `dotnet format` aynı anda başladı, 12–16 numaralı komutlar 0,4 saniye içinde başladı. (3) Baz log'un sha256'sını tester yeniden hesaplamadı. Hash'i insan sonradan kendisi doğruladı.
- **Negatif hedefler:** Faz 2'nin 17 özgün negatif hedefinden yalnızca biri (`git stash list --grep=LIVEPROBE-2e`) canlı ortamda bloklandı. Kalan 16 hedef uçtan uca DOĞRULANMAMIŞ durumda. Ayrıntılar "Kavramlar" bölümünde.

## Ne yaptık

Faz 2, pipeline'daki "şunu yapma" kurallarını ajanların prompt'larında bıraktı ve onlara, Claude Code'un her araç çağrısından önce çalıştırdığı hook betikleriyle bir uygulama katmanı ekledi: coder için yalnızca okuma yapan git komutlarına izin veren `coder-bash-guard.js`, tester ve explainer için profil başına bir komut izin listesi tutan `bash-allowlist.js`, coder dışındaki yedi ajan için yazmayı tek bir dizinle sınırlayan `write-scope.js`. Sekiz ajan dosyası artık modeli tam model kimliğiyle sabitliyor ve effort düzeyini, kendi `tools` listesini ve bir `hooks:` bloğunu taşıyor; okuma ağırlıklı beş ajan bunlara ek olarak LSP ve Microsoft Learn MCP araçlarını aldı. Hook'ların karar mantığını 131 yeni test sınıyor (toplam 216 test), sekiz mutation proof bu testlerin belirli gerilemeleri yakaladığını gösteriyor ve bazı ajan-hook bağlantılarını canlı oturumlardan alınan araç ve hook-log kayıtları belgeliyor. Ürün koduna (`src/`, `tests/`, `db/`) dokunulmadı ve `dotnet test` hâlâ 638 test sayıyor. Önceden bu kurallar yalnızca prompt metni olarak vardı ve Faz 1'deki tester olayı yazılı bir kuralın tek başına tutmadığını göstermişti.

## Yeni giren teknolojiler

### Claude Code PreToolUse hook'ları
- **Ne işe yarar:** Claude Code bir aracı (`Bash`, `Write`, `Edit` gibi) çalıştırmadan hemen önce senin tanımladığın bir komutu çalıştırır. Çağrının ayrıntılarını bu komuta JSON olarak stdin'den verir. Komut 2 koduyla çıkarsa araç çalışmaz ve komutun stderr'e yazdığı metin modele geri döner. 0 koduyla çıkarsa araç normal şekilde çalışır. Frontend'den bir benzetme: isteği iptal edebilen bir axios request interceptor'ı düşün. Tek fark, bu interceptor model ile işletim sistemi arasında duruyor.
- **Bu projede nerede:** Global hook `path-guard`, `.claude/settings.json` içinde `hooks.PreToolUse` altında. Rol başına hook'lar `.claude/agents/*.md` dosyalarının frontmatter'ındaki `hooks:` bloğunda. Betikler `.claude/hooks/coder-bash-guard.js`, `.claude/hooks/bash-allowlist.js` ve `.claude/hooks/write-scope.js`; ortak kod `.claude/hooks/lib/guard-common.js`'te.
- **Alternatifi neydi:** Üç alternatif vardı.
  - Yalnızca prompt kuralları. Faz 1'de (`9a5f1db`) tester talimatın dışına çıktı ve yazılı kural tek başına tutmadı.
  - Yalnızca `permissions.deny` kalıpları. Bunlar bu repoda `settings.json`'da, yani oturum düzeyinde duruyor. "Bu ajan yalnızca `docs/journal/` altına yazar" gibi rol başına bir kuralı burada frontmatter hook'u kuruyor.
  - Gerçek bir sandbox, yani bir konteyner ya da ayrı bir kullanıcı. Modelin kendi yazdığı kodu çalıştırdığı durumda gerçek sınırı yalnızca bu çizer. Plan bunu kapsam dışında bıraktı.
  - Bu seçimle verdiğimiz şey şu: hook'lar metin üzerinde çalışan sezgisel kurallar (string heuristic), bir sandbox değiller.
- **Nerede okunur:** https://code.claude.com/docs/en/hooks

### Subagent frontmatter'ı: `tools`, `model`, `effort`
- **Ne işe yarar:** Her ajan dosyasının en üstündeki YAML bloğu üç şeyi belirler: ajanın hangi araçları görebileceğini, hangi modelle çalışacağını ve ne kadar akıl yürütme çabası (effort) harcayacağını. Tester dışındaki yedi ajan `claude-opus-5-5` ile çalışıyor; effort `xhigh` ya da `max`. Tester `claude-sonnet-5-5` ile ve effort `low` ile çalışıyor.
- **Bu projede nerede:** `.claude/agents/*.md` dosyalarının ilk 13–17 satırı.
- **Alternatifi neydi:** `opus`/`sonnet` gibi takma adlar ya da ana oturumun modelini devralmak. Takma ad zamanla başka bir modeli gösterebilir; tam kimlik ise modeli sabitler. Tam kimlik reddedilirse P2.1'in yedek kuralı takma ada geri dönmek. Tester'ı ucuz modele almanın bedeli var: plan da "model seçimi doğru raporlamayı garanti etmez" diyor. Nitekim tamamlanmış tester raporundaki üç ifadeyi insan kaydı düzeltti (aşağıda, "Kayıt türleri"). Bir de ana oturum ajanı çağırırken bir `model` parametresi verirse frontmatter'daki model ezilir. Bu yüzden delegasyon metinleri "do not pass a model parameter" diyor.
- **Nerede okunur:** https://code.claude.com/docs/en/sub-agents

### Node.js test runner (`node:test`) ve `node:assert/strict`
- **Ne işe yarar:** Node'un kendi içinde gelen test çalıştırıcısı. Jest ya da Vitest'in yaptığı işi ek paket kurmadan yapar. `node --test <dosyalar>` dosyaları çalıştırır, her test için bir `✔`/`✖` satırı ve sonda bir özet basar. Bu sistem Faz 1'de geldi; Faz 2 buna üç test dosyası ekledi.
- **Bu projede nerede:** `.claude/hooks/tests/coder-bash-guard.test.js`, `.claude/hooks/tests/bash-allowlist.test.js` ve `.claude/hooks/tests/write-scope.test.js`. Yardımcı dosya `.claude/hooks/tests/run-hook.js`.
- **Alternatifi neydi:** Jest ya da Vitest. İkisi de bu .NET reposuna bir `package.json`, bir `node_modules` ve bir bağımlılık zinciri eklerdi. Hook'lar ise bilerek "CommonJS, yalnızca Node built-in" kuralıyla yazıldı. Verdiğimiz şeyler: hazır mocking ve snapshot araçları yok. Ayrıca `run-hook.js`'teki `runHook`, her hook çağrısı için ayrı bir Node süreci başlatıyor; süitte hook çağırmayan başka testler de var ve 216 testin tamamı yaklaşık 7 saniye sürdü.
- **Nerede okunur:** https://nodejs.org/api/test.html ve https://nodejs.org/api/assert.html

### LSP ve `csharp-ls`
- **Ne işe yarar:** LSP (Language Server Protocol), bir editörün bir dil sunucusuna "bu sembol nerede tanımlı?" ya da "bu dosyada hangi semboller var?" gibi sorular sorduğu standart bir protokol. VS Code'da F12 ile "Go to Definition" yaptığında arka planda bu konuşma döner. Burada aynı soruları bir ajan soruyor. `csharp-ls` de C# için yazılmış bir dil sunucusu.
- **Bu projede nerede:** `.claude/settings.json` içindeki `enabledPlugins` alanında `csharp-lsp@claude-plugins-official`. `csharp-ls` global bir dotnet aracı olarak kurulu (H2.1). Araç; researcher, planner, plan-reviewer, code-reviewer ve db-reviewer'ın `tools:` satırında. Coder, tester ve explainer'da yok; plan bu kararın gerekçesini yazıyor.
- **Alternatifi neydi:** Yalnızca Grep. Grep metin arar, sembol çözümlemez. P2.6'da `Result` için yapılan `workspaceSymbol` sorgusu 29 sembol döndürdü. Bunlardan hangisinin `Envanex.Domain.Common.Result` olduğunu Grep bilemez; LSP ise sembolleri çözümlemek için tasarlandı. Kayıttaki `goToDefinition` çağrısı `Result`'ın kendi bildirimi üzerinde yapıldı (`src/Envanex.Domain/Common/Result.cs`, satır 3, karakter 14) ve aynı konumu döndürdü: `Defined in src/Envanex.Domain/Common/Result.cs:3:14`. Bu kayıt, işlemin o konumda çalıştığını doğruluyor; bir kullanım yerinden tanıma gitmeyi (use-to-definition) doğrulamıyor. Verdiklerimiz:
  - İlk çağrı yavaş. C2.7 ve C2.8'de 12–13 saniye sürdü, P2.6'daki ilk `workspaceSymbol` çağrısı ise yaklaşık 21 saniye.
  - Çalışma zamanında yalnızca code-reviewer × LSP çifti doğrulandı. Diğer dört profilin LSP'ye erişimi doğrulanmadı ve bu sınır planda bilerek kabul edildi.
- **Nerede okunur:** https://microsoft.github.io/language-server-protocol/ ve https://github.com/razzmatazz/csharp-language-server

### MCP ve Microsoft Learn MCP sunucusu
- **Ne işe yarar:** MCP (Model Context Protocol), bir modelin dış bir servisin araçlarını standart bir yolla çağırmasını sağlar. Microsoft Learn sunucusu üç araç sunuyor: `microsoft_docs_search`, `microsoft_docs_fetch` ve `microsoft_code_sample_search`. Bunlarla bir ajan, bir .NET API'sinin gerçekten var olup olmadığını resmî dokümana bakarak kontrol edebiliyor. Böylece "uydurma API" riski azalıyor.
- **Bu projede nerede:** Sunucu tanımı `.mcp.json` içinde: `microsoft-learn`, `"type": "http"`. `.claude/settings.json` içindeki `permissions.allow` listesinde üç tam araç adı var. Araçlar ayrıca ajanların `tools:` satırlarında. Aynı dosyadaki `"disableClaudeAiConnectors": true` ayarı claude.ai hesabından senkronize edilen iki bağlayıcıyı kapatıyor; bunlardan biri yazma araçlarına sahip. P2.7'de `claude mcp list` yalnızca `microsoft-learn: ... ✔ Connected` satırını gösterdi.
- **Alternatifi neydi:** İki seçenek reddedildi.
  - `microsoft-docs` plugin'i. Reddedildi, çünkü skill'leri fork modunda çalışıyor ve `npx` yedekleri var.
  - Sunucu düzeyinde tek bir izin, yani `mcp__microsoft-learn`. Reddedildi, çünkü sunucunun ileride ekleyeceği her aracı da kapsardı.
  - Verdiklerimiz:
    - Dış bir HTTP servisine bağımlı hale geldik.
    - Çalışma zamanında yalnızca researcher × `microsoft_docs_search` çifti doğrulandı. Sorgu 10 sonuç döndürdü ve hepsinin `contentUrl` alanı `https://learn.microsoft.com/` ile başlıyordu.
    - `microsoft_code_sample_search` yalnızca insanın `/mcp` kaydına dayanıyor; incelenen kayıtlarda bu araca yapılmış bir çağrı yok.
- **Nerede okunur:** https://modelcontextprotocol.io/, https://code.claude.com/docs/en/mcp ve https://learn.microsoft.com/training/support/mcp

## Kavramlar

### Ajan, araç ve araç çağrısı
Ajan (subagent), `.claude/agents/<ad>.md` dosyasıyla tanımlanan bir Claude örneği. Kendi sistem prompt'u, modeli ve araç listesi var. Araç (tool), modelin dış dünyaya dokunabildiği tek yol: `Read`, `Write`, `Edit`, `Bash`, `Grep` gibi. Model bir araç "çağırdığında" çağrıyı Claude Code yürütür ve sonucu (`tool_result`) modele geri verir. Hook'lar tam bu iki adımın arasına girer.

### Hook girdisi, çıkış kodu ve stderr
Claude Code hook komutunu bir alt süreç olarak başlatır ve çağrının ayrıntılarını tek bir JSON belgesi olarak stdin'e yazar. Bu belgede `tool_name`, `tool_input` (Bash için `command`, Write için `file_path`) ve oturum/ajan kimlikleri var. `guard-common.js` içindeki `readHookInput` bu girdiyi okur. Çıkış kodu kararın kendisidir: 0 izin, 2 blok demek. Blok durumunda `block` fonksiyonu `Blocked by <hook>: <sebep> -> <hedef>` satırını stderr'e yazar ve `process.exit(2)` çağırır.

Bash hook'ları hedefi `runGuard`'a üçüncü argüman olarak verilen `getCommand` ile okur; `write-scope` ise varsayılan `getTargetPath`'i kullanır. Bunun canlı bir örneği P2.2'nin 2e kaydı: coder'ın `git stash list --grep=LIVEPROBE-2e` çağrısının `tool_result`'ı `PreToolUse:Bash hook error: [...]: Blocked by coder-bash-guard: git subcommand "stash" is not in the read allowlist -> ...` oldu. Aynı anda hook log'una bir `block` satırı düştü.

### Kapalı başarısızlık (fail closed)
Bir güvenlik kontrolü beklenmedik bir durumda "izin ver" yerine "engelle" diyorsa, kapalı başarısız oluyor demektir. `runGuard` içindeki `run().catch(...)` bunu yapar: JSON bozuksa, proje dizini bulunamazsa ya da `decide` hata fırlatırsa sonuç `guard error: ...` sebebiyle bir bloktur. Her test dosyasındaki `blocks on malformed JSON` testi bu davranışı sabitliyor.

Ajan dosyalarındaki komutların sonundaki `|| exit 2` aynı fikri bir katman dışarı taşır. Claude Code yalnızca 2 kodunu blok sayar; diğer sıfırdan farklı kodları engellemeyen bir hata olarak geçer. `node` hiç başlayamazsa ya da betik `runGuard`'a varmadan çökerse, çıkış kodu bu ek sayesinde 2'ye çevrilir. Bedeli şu: hook'taki her hata, işi durduran bir yanlış pozitife dönüşür.

### İki katman: izin kuralları ve hook'lar
`.claude/settings.json` içindeki `permissions.allow` listesi, hangi komutların izin istemeden çalışacağını kalıplarla belirler. `permissions.deny` listesi de hangi komutların hiç çalışmayacağını belirler; `Bash(git stash *)` buna bir örnek. Bu repoda deny listesi Bash ve PowerShell için aynalanmış 47'şer kalıptan oluşuyor. C1.9 bu aynalamayı `mirror-ok 47` çıktısıyla kontrol ediyor. Hook'lar bunun üstünde, rol başına çalışan ikinci bir katman; bir komutun çalışması için iki katmandan da geçmesi gerekiyor. İki katmandan hangisinin önce çalıştığı sorusu (C2.4) yalnızca 2e komutu için gözlendi. Orada `git stash *` deny kalıbı da vardı, ama `coder-bash-guard`'ın log satırı yine de düştü.

### İzin listesi (allowlist) ve yasak listesi (denylist)
Denylist "şunlar dışında her şey serbest" der. Unuttuğun her kalıp açık bir kapı demektir. Allowlist ise "yalnızca şunlar serbest" der. Unuttuğun her kalıp kapalı bir kapı, yani gürültülü bir işlev hatası olur. Bu, 0008'deki "varsayılan olarak kapalı" yetkilendirmeyle aynı mantık.

Coder'ın okuma-git listesi ve `bash-allowlist.js` birer allowlist. `settings.json`'daki deny listesi ise yaygın yazılan yıkıcı komut biçimlerini yakalamaya çalışan bir denylist. Kapsamadığı her biçim planda adı konmuş bir artık risk (residual risk) olarak kayıtlı.

### Token bazında eşleme ve giriş türleri
`bash-allowlist.js` komutu boşluklardan parçalara (token) böler ve izin girişinin token'larıyla birebir karşılaştırır (`startsWithTokens`, 97–99). Bu yüzden `git diff` girişi `git diff --stat`'ı kabul eder ama `git difftool`'u kabul etmez. Düz bir metin öneki kontrolü, yani `startsWith('git diff')`, ikisini de kabul ederdi. React Router'da `/admin` kuralının yanlışlıkla `/administrator`'ı da yakalaması gibi.

Girişler dört türde:
- `exact`: ek token kabul etmez. Örnek: `bash .claude/skills/gate/scripts/gate.sh`.
- `prefix`: ön kontrolü ve `--output` kuralını geçen her ek token'ı kabul eder. Örnekler: `git log`, `grep`, `cat`.
- `dotnet`: yalnızca listelenmiş ek token'ları kabul eder. Liste `tester.md`'de yazılı: `-warnaserror`, `--no-restore`, `--filter`, `-v`/`--verbosity`, `src/` ya da `tests/` altında bir proje yolu ve `2>&1`.
- `node-test`: yalnızca `.claude/hooks/tests/` altındaki test dosyalarını kabul eder.

Pipe'tan (`|`) sonraki her parça `grep`, `head`, `tail` ya da `wc` ile başlamak zorunda.

### Ön kontrol (pre-check) ve tırnakların dikkate alınmaması
`precheck` fonksiyonu (62–95), komutu ayrıştırmadan önce yasak karakterleri arar: satır sonu, `;`, `&&`, `||`, ters tırnak, `$(`, `<(` ve `>(`, tek başına `&`, ve `2>&1` dışındaki her `>`. Bunu yaparken tırnakları bilerek hesaba katmaz (quote-unaware), bu yüzden `grep "a;b" x` gibi bir komut da bloklanır. Pipe'a göre bölme de tırnakları hesaba katmıyor. `grep -E "Passed!|Failed!"` testi bu belgelenmiş yanlış pozitifi sabitliyor. Statik okumayla, Validation'ın 7. komutu da (`grep -cE 'Bash\((git switch \*|...)\)' ...`) aynı nedenle tester'a yasak ve "yalnızca insan" diye işaretli. Daha temel bir sınır da var: hook komutun metnini değerlendirir, shell'in o metinle ne yapacağını değil. Glob'ları genişletmez, tırnakları kaldırmaz.

### `\b` kelime sınırı ve coder-bash-guard'ın yanlış pozitifleri
`coder-bash-guard.js` komutun yalnızca başına değil, tamamına bakar. Büyük/küçük harfe duyarsız `\bgit(\.exe)?\b` regex'inin her eşleşmesini ayrı bir git çağrısı sayar. Böylece `cd src && git add x`, `bash -c "git commit -m x"` ve `node -e "...execSync('git push')"` gibi sarmalanmış çağrılar da yakalanır.

`\b` bir kelime sınırıdır: bir harf ya da rakam ile harf olmayan bir karakter arasındaki geçiş. Bu yüzden `digit`, `.gitignore` ve `.github` eşleşmez, ama `.git/hooks` ve `tools/git/x` eşleşir. Her eşleşmeden sonra hook izin verilen birkaç global seçeneği atlar ve alt komutu (subcommand) okuma listesinde arar: `status`, `diff`, `log`, `show`, `merge-base` ve diğerleri. `branch` alt komutu yalnızca okuma argümanlarıyla geçer. `gh` kelimesi komutun her yerinde bloklanır.

Bu yaklaşımın bedeli belgelenmiş yanlış pozitifler. `ls .git/hooks` "unparseable git call" sebebiyle bloklanır. `grep -rn "git" docs` de bloklanır, çünkü `docs` alt komut olarak okunur. Böyle durumlarda coder bunların yerine Grep ya da Glob aracını kullanmalı.

### Yazma kapsamı ve yol normalizasyonu
`write-scope.js <dizin>` bir Write ya da Edit hedefini yalnızca o dizinin altındaysa kabul eder. Kapsam argümanı yoksa ya da hedef proje dışındaysa blok verir.

Karşılaştırmadan önce iki yol da `normalizePath`'ten geçer:
- ters bölüler `/` olur,
- git-bash'in `/c/...` biçimi `c:/...` olur,
- göreli yollar proje dizinine eklenir,
- `.` ve `..` parçaları çözülür,
- her şey küçük harfe çevrilir, çünkü Windows'un dosya sistemi büyük/küçük harf ayırmaz.

Böylece `Thoughts\Shared\Reviews\x.md` izin alır. `thoughts/shared/reviews/../plans/x.md` ise `.../plans/x.md` yoluna çözülür ve bloklanır. Karşılaştırma `${scope}/` ile, yani sonunda `/` olan kapsamla yapılıyor. Bu sondaki `/`, `reviews-evil` gibi kardeş dizinleri dışarıda tutar.

Sınırı şu: bu yalnızca metin üzerinde çalışan bir mantık. 8.3 kısa adları, junction'ları ve link'leri, sonu noktayla biten adları ya da NTFS stream eklerini çözmez. Plan bunları DOĞRULANMAMIŞ (UNVERIFIED) artık riskler olarak kaydediyor. Coder'ın kendi write-scope hook'u yok; yalnızca global `path-guard` (`bin/`, `obj/`, `.git/`, `.env` ve benzerleri) onu da kapsıyor.

### Redaction ve hook log'u
Her karar `TestResults/hook-log/hooks.jsonl` dosyasına tek bir JSON satırı olarak eklenir (`logDecision`, 216–237). Satırda zaman, hook adı, karar, sebep, araç adı, hedef ve ajan/oturum kimlikleri bulunur. Hook adı argümanını da içerir: `write-scope:docs/journal` ya da `bash-allowlist:tester` gibi.

Bash hook'ları artık tam komut satırlarını logladığı için, Faz 1'den gelen `redactSecrets` fonksiyonu önce bazı değerleri `***` ile maskeler: parola atamaları, token'lar, `sqlcmd -P` değeri, `user-secrets set` değeri ve URL içindeki kullanıcı:parola bilgisi. Hedef metin ancak bundan sonra 300 karaktere kesilir. Bu sıra önemli ve M1.28 onu sabitliyor. Log yazılamasa bile karar değişmez: kodda `// Logging must never change a decision.` yazıyor.

### Test izolasyonu: `ENVANEX_HOOK_LOG_DIR` ve `LIVEPROBE`
`run-hook.js`'teki `runHook`, hook çağıran testlerde hook'u her çağrı için ayrı bir Node süreci olarak (`spawnSync` ile) başlatır ve JSON'u stdin'e verir. Bunu yaparken `ENVANEX_HOOK_LOG_DIR`'ı yeni bir geçici dizine ayarlar, böylece testler projenin gerçek log'una hiç yazmaz. Canlı prob hedefleri `LIVEPROBE-<id>` etiketi taşır ve hiçbir test dosyasında bu kelime geçmez (C1.7, Validation komutu 3). Bu yüzden projenin log'unda `LIVEPROBE` içeren bir satır ancak canlı bir hook'tan gelebilir. Birim testin kanıtını canlı gözlemin kanıtından ayıran mekanizma bu.

### Assertion ve assertion kütüphanesi
Assertion, testin "bu doğru olmalı" dediği satırdır. Koşul yanlışsa test kırmızıya döner. Burada kullanılan kütüphane Node'un kendi `node:assert/strict` modülü; Jest'teki `expect`'in karşılığı gibi düşünebilirsin. `assert.equal` strict modda `===` ile karşılaştırır ve koşul tutmazsa `AssertionError [ERR_ASSERTION]` fırlatır.

Test dosyalarında iki yardımcı fonksiyon var:
- `assertBlocked` üç şeyi kontrol eder: çıkış kodunun 2 olması, stderr'de `Blocked by <hook>:` geçmesi ve beklenen sebep metninin geçmesi.
- `assertAllowed` beş şeyi kontrol eder: çıkış kodunun 0 olması, log'da tek bir satır olması, o satırda `decision === 'allow'` olması, hook adının doğru olması ve (Bash hook'larında) log'daki hedefin komutla aynı olması.

Sebep assertion'ının neden önemli olduğunu M2.5 gösterdi. `>` ön kontrolü bir kopyada silindiğinde `echo x > src/a.cs` yine bloklandı, çünkü `echo` zaten izin listesinde yok. Değişen tek şey sebep oldu: `redirection ...` yerine `command is not in the tester allowlist`. Yalnızca çıkış koduna bakan bir test yeşil kalırdı. Testi kırmızıya çeviren, `bash-allowlist.test.js:49`'daki sebep assertion'ı oldu.

### Karar testi, mutation proof ve canlı bağlantı gözlemi
Faz 2 üç ayrı kanıt türü kullanıyor ve her biri farklı bir soruya cevap veriyor:

| Kanıt | Bu dalda | Gösterdiği | Göstermediği |
|---|---|---|---|
| Karar testi (birim test) | `node --test .claude/hooks/tests/*.test.js`: 216 test, 131'i Faz 2'nin | Hook betiğinin verilen bir JSON girdisine hangi kararı ve hangi sebebi verdiği | Claude Code'un o betiği gerçekten çağırdığı; ajan dosyasındaki YAML'ın hook'u gerçekten kurduğu |
| Mutation proof | M2.1–M2.8 | Planlanan belirli bir gerileme yapıldığında hedef testin kendi assertion'ıyla kırmızıya döndüğü | Canlı bağlantı; listede olmayan gerilemeler |
| Canlı bağlantı gözlemi | 2e blok kaydı; R8 ve R9 Write izin kayıtları; tester'ın P2.5 işlemleri | Belirli bir ajanın belirli bir çağrısında adı geçen hook'un çalıştığı ve kararını logladığı | Hiç çağrılmamış bir negatif girdinin bloklanacağı; ajanın yazdığı içeriğin doğru olduğu |

Mutation testing, kodu bilerek bozup testin bunu fark edip etmediğine bakmak demek. Test kırmızıya dönmüyorsa o davranışı korumuyor demektir. Stryker gibi araçlar yüzlerce mutasyonu otomatik üretir. Burada ise her mutasyon elle seçilmiş tek bir değişiklik. "Kırmızı" sonuç yalnızca hedef testin kendi assertion'ı mutasyonun amaçlanan etkisiyle patlarsa sayılıyor. Hook'un çöküp `runGuard` tarafından `guard error` bloğuna çevrilmesi ya da test sayısının düşmesi sayılmıyor.

| ID | Mutasyon | Hedef test | Kayıtlı kırmızı |
|---|---|---|---|
| M2.1 | coder-bash-guard yalnızca komutun ilk token'ına baksın | `cd src && git add x`, `bash -c "git commit -m x"` | 6 test |
| M2.2 | okuma listesine `commit` eklensin | `git commit -m x` | 5 test |
| M2.3 | write-scope önekindeki sondaki `/` kaldırılsın | `thoughts\shared\reviews-evil\x.md` | 1 test |
| M2.4 | write-scope `..` parçalarını çözmesin | `thoughts/shared/reviews/../plans/x.md` | 1 test |
| M2.5 | `>` ön kontrolü silinsin | `echo x > src/a.cs` | 3 test |
| M2.6 | `node --test` argüman kontrolü silinsin | `node --test` ile `..` traversal | 2 test |
| M2.7 | token bazında eşleme yerine metin öneki kullanılsın | `git difftool` | 1 test |
| M2.8 | dotnet girişinden sonra her token kabul edilsin | üç dotnet blok testi | 9 test |

M2.1 ve M2.2 canlı dosya üzerinde, her biri kendi coder turunda uygulandı ve ters Edit ile geri alındı. M2.3–M2.8 ise `ce5e7cf`'ten repo dışına çıkarılan atılabilir kopyalarda, insanın çalıştırdığı bir runner ile yapıldı (Revision 7). Her kanıtın kabulü insanın ayrı bir değerlendirme kaydına bağlı.

### 17 negatif hedef ve "uçtan uca doğrulanmamış"
Faz 2'nin probları başta 17 negatif hedef tanımladı:
- coder için 5 hedef (2a–2e),
- yazma kapsamları için 8 hedef: yedi ajanın her biri için `src/LIVEPROBE-4-<ajan>.txt` ve code-reviewer için ek olarak `thoughts/shared/reviews-probe/LIVEPROBE-4-code-reviewer.md`,
- izin listeleri için 4 hedef (5a, 5b, 5f, 5d).

Canlı gözlem bu hedefleri üç kategoriye ayırdı:
- **Tam canlı blok:** 1 hedef (2e).
- **İstendi ama ajan araç çağırmadan reddetti, sonuç belirsiz:** 6 hedef (2a–2d ve iki code-reviewer hedefi). Örneğin coder, `git commit --dry-run` isteğini kendi "asla git commit çalıştırmam" kuralı yüzünden reddetti.
- **Hiç istenmedi:** 10 hedef.

Bir modelin bir isteği reddetmesi hook bloğu sayılmaz: araç çağrılmadığı için hook hiç çalışmadı. Revision 8 iki şey yaptı:
- Ajanlardan rolleriyle çelişen negatif girdiler istemeyi bıraktı.
- Bu 16 hedefin gerçek ajan → araç → hook zincirinde reddedildiğinin DOĞRULANMAMIŞ kalmasını bilinçli bir sınır olarak kabul etti.

Onların yerine davranış kapsamını karar testleri veriyor. Bu kapsam bayt bayt aynı girdiyle sağlanmıyor, çünkü hiçbir test dosyası `LIVEPROBE` içeremez. Bu 16 satırı kabul eden insan değerlendirmesi ve kapsam defteri R9.11'e ait; ikisi de henüz yazılmadı.

### Kayıt türleri
Planın Revision 6'sı her kanıt kaydını dört türden birine yerleştiriyor:
- **Mekanik sonuç kaydı:** bir komutun ya da araç çağrısının, shell'in ya da araç çalışma zamanının yakaladığı gerçek çıktısı ve çıkış kodu.
- **İnsan gözlemi:** "izin istemi çıkmadı" gibi bir tespit.
- **İnsan kararı:** READY_TO_PUSH gibi.
- **Model raporu:** bir ajanın ya da ana oturumun yazdığı metin, "verbatim" (aynen) diye sunulsa bile.

Model raporu kanıt sayılmıyor; incelenmesi gereken bir iddia olarak ele alınıyor. Bu dalda bunun somut bir sonucu oldu: tamamlanmış tester raporundaki üç ifadeyi insan kaydı düzeltti.
1. Tester, baz hash'in ön kontrolde (preflight) kontrol edildiğine güvendiğini yazdı. Oysa hash'i devam çalıştırmasından sonra insan doğruladı.
2. "Beş R10 testi beş ayrı dala ulaşıyor" ifadesi yanlış. Beş test dört karar noktasını kapsıyor, çünkü `<(` ve `>(` aynı satırda (81) kontrol ediliyor.
3. "Üst üste binen komutlar hiçbir şey yazmaz" denemez. Testler ve build araçları geçici ya da çıktı dosyaları yazabilir. Takip edilen dosyaların çalışma ağacı ise temiz kaldı.

Kayıtlar farklı Claude Code sürümlerinde toplandı (örneğin 2.1.288, 2.1.292 ve 2.1.296). Plan bunları karma sürümlü kanıt olarak kabul ediyor: bir sürümdeki gözlem başka bir sürüm için kanıt sayılmaz.

### Tehdit modeli: işbirlikçi ajan
Plan, guard'ların neye karşı olduğunu tek bir cümleyle sınırlıyor: işbirlikçi bir ajanın, yani kuralları çiğnemeye çalışmayan bir ajanın, kazara yapacağı yıkıcı işlemleri durdurmak. Guard'lar, modelin kendi yazıp çalıştırdığı koda karşı bir sınır değil. Tester'a izin verilen `dotnet test` test projelerindeki her kodu çalıştırır; `node --test` de test dosyasındaki her şeyi çalıştırır. Plan, bu açığı daha fazla kalıpla değil ancak bir sandbox ile kapatılabilecek bir artık risk olarak tanımlıyor; kaydı Faz 4'te yazılacak. Bu yüzden bu hook'ları bir "güvenlik sınırı" olarak okuma. Kazayı fark edilir hale getiren bir korkuluk olarak okumak daha doğru.

### Kararı HEAD'e ve dala bağlamak
Faz 2'den önce incelemeciler ve tester kararlarını yalnızca sohbete yazıyordu. Artık her biri bir dosya yazıyor:
- İnceleme dosyasının ilk beş satırı sabit: `Verdict:`, `Reviewer:`, `Scope:`, `Range:`, `Head:`.
- Tester dosyasının ilk üç satırı sabit: `Verdict:`, `Head:`, `Branch:`.

`Head:` satırı kararı belirli bir commit'e bağlar. Kararın ardından yeni bir commit gelirse, eski karar o kodu görmemiş demektir. R9.10 tetikleyicisi bunu kullandı: saklanan raporun `Head:` değerinin hem çalıştırmadan önce kaydedilen HEAD'e hem de o anki `git rev-parse HEAD` çıktısına eşit olması gerekiyordu. Ayrıca raporun sha256'sı, dosya taşınmadan önce alınan hash'le aynı olmalıydı.

## Komutlar ve ne yaptıkları

Aşağıdakiler; ön tester çalıştırmasının (R9.9) gerçekten çalıştırdığı komutlar, bu kaydı yazarken çalıştırılan komutlar ve insanın kayıt yöntemi. Tester'ın sayısal çıkış kodları görünmedi. "Beklenen" değerler planın koşullarından geliyor.

- `node --test .claude/hooks/tests/*.test.js`: Node'un test çalıştırıcısını beş test dosyasının hepsinde çalıştırır. `*`'ı Node değil Bash genişletir. `ca46009`'daki özet: `tests 216`, `suites 0`, `pass 216`, `fail 0`. `suites 0` bilinçli bir tasarım seçimi: testler `describe` olmadan düz `test()` çağrılarıyla yazıldı, böylece bir testin kimliği yalnızca adı oluyor.
- `node --test .claude/hooks/tests/write-scope.test.js`: P2.5'te tester'a düşen işlem. Tek bir dosyayı çalıştırır; 17 testin hepsi geçti.
- `grep -c LIVEPROBE .claude/hooks/tests/*.js .claude/hooks/tests/fixtures/*.js`: `-c` bayrağı her dosya için eşleşen satır sayısını basar. Yedi dosyanın hepsi `:0` döndü. Hiç eşleşme olmadığı için grep'in çıkış kodu 1, ve burada beklenen sonuç tam olarak bu. Yani "exit 1 = başarısızlık" her zaman doğru değil; hangi kodun başarı sayılacağını komutun koşulu belirler.
- `grep -c $'\r' <dosyalar>`: `$'\r'` Bash'in ANSI-C tırnaklaması; argümana gerçek bir satırbaşı (CR) karakteri koyar. Sayının 0 çıkması dosyada yalnızca LF satır sonu olduğunu gösterir (`.gitattributes` kuralı).
- `dotnet format --verify-no-changes`: Biçimlendirme gerekiyorsa dosyaya yazmak yerine sıfırdan farklı bir kodla çıkar. Çıktı boştu.
- `dotnet build -warnaserror`: Her uyarıyı hata sayar. Sonuç: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test`: Üç test assembly'sini çalıştırır: 120 + 126 + 392 = 638 testin hepsi geçti. Ürün test sayısı değişmedi.
- `git diff --name-only main...HEAD -- src tests db`: Üç nokta (`...`), `main` ile `HEAD`'in ortak atasından (merge-base) `HEAD`'e kadar olan farkı verir. `--name-only` yalnızca dosya adlarını basar. `--` işaretinden sonraki yollar farkı bu dizinlerle sınırlar. Çıktı boş: dal ürün koduna dokunmadı.
- `git diff --name-only be6477e..HEAD -- <Faz 1 test dosyaları>`: İki nokta (`..`), iki commit arasındaki doğrudan farkı verir. Çıktı boş: Faz 1'in testleri ve yardımcı dosyaları Faz 2'de değişmedi.
- `ls .claude/hooks/tests/*.test.js`: Tam olarak beş test dosyası listelendi.
- `grep -n "model: haiku" .claude/agents/*.md`: Hiçbir ajanın Haiku kullanmadığını doğrular (C2.5). Beklenen sonuç boş çıktı ve exit 1.
- `git log --oneline -3 --grep=LIVEPROBE-5e`: P2.5'te explainer'a düşen işlem. Bu kaydı yazan explainer bunu tek bir Bash çağrısıyla bir kez çalıştırdı. `--grep` commit mesajlarında regex arar, `-3` en fazla üç sonuç verir, `--oneline` her commit için kısa SHA ile başlığı basar. Araç hata döndürmedi ve çıktı boştu, yani dalda bu etiketi taşıyan bir commit mesajı yok. Benim Bash aracım da sayısal çıkış kodunu göstermedi. Bu çağrıyı hook log'uyla eşleştirmek insanın işi; burada öyle bir iddiada bulunmuyorum.
- `git diff main...HEAD --stat`: Explainer yönteminin ilk adımı; her dosya için eklenen ve silinen satırların özetini basar. Dal Faz 1'i de içerdiği için listede `guard-common.js` ve `path-guard.js` de var.
- `grep -H '^tools:' .claude/agents/*.md`: P2.6'dan önce çalıştırıldı. `-H` her satırın başına dosya adını ekler, `^` eşleşmeyi satır başına sabitler. Çıkan sekiz satırın onaylı listeyle aynı küme olması yalnızca yapılandırmanın onaylandığı gibi olduğunu gösterir; araçların gerçekten çalıştığını göstermez.
- `claude mcp list`: P2.7'de çalıştırıldı. Yapılandırılmış MCP sunucularını ve bağlantı durumlarını listeler.
- `sha256sum <dosya>`: Dosya içeriğinin parmak izini üretir. Rapor saklama prosedürü, tester raporunu sabit yolundan `validation/` altına taşımadan önce ve taşıdıktan sonra hash'i karşılaştırır. R9.10 tetikleyicisi de bu hash'in (`54cfae98...`) eşitliğine dayanıyor.
- İnsanın `run` fonksiyonu (plan, Faz 2 Validation): Her komutu `bash -c "$1" < /dev/null >> "$E" 2>&1` ile çalıştırır ve çıkış kodunu `rc=$?` ile yakalar. Log'a önce `$ <komut>` satırını, sonra çıktıyı, en son da `exit=<kod>` satırını yazar.
  - `< /dev/null`, komutun klavyeden girdi beklemesini engeller.
  - `2>&1`, stderr'i stdout ile aynı dosyaya yönlendirir.
  - Son Validation bu fonksiyonla yapılacak: insanın elinde, tek bir log dosyasına. Ön tester çalıştırması bunun yerine geçmez.

## Dikkat edilen tuzaklar

1. **Yazılı kural tek başına yetmiyor.** Faz 1'de (`9a5f1db`) tester komutları değiştirdi, `dotnet` çıktısını `tail`'e pipe'ladı ve grep'in çıkış kodunu 0 olarak raporladı. Faz 2 kuralları hook'lara taşıdı. Ama Validation'ı yine insanın `run` log'una bağladı: tester raporu bir kapı değil, bir iddia.
2. **Pipe çıkış kodunu yutar.** Bash'te `dotnet test 2>&1 | tail -20` komutunun çıkış kodu, son komutun, yani `tail`'in çıkış kodudur. `pipefail` açık değilse `dotnet test` başarısız olsa bile 0 görürsün. Tester'ın izin listesi `grep`, `head`, `tail` ve `wc`'ye pipe'a izin veriyor; plan pipe için ek bir kural eklemedi. Bu yüzden asıl kapı, insanın pipe kullanılmayan Validation log'u.
3. **`-p:PreBuildEvent=<komut>` keyfi komut çalıştırır.** C2.6'da insan `dotnet build -p:PreBuildEvent="echo PWNED > pwned.txt"` komutunu çalıştırdı ve dosya `bin/Debug/net10.0/` altında oluştu. Kayıttaki sonuç "exec: yes" (tek çalıştırma, SDK 10.0.400, Windows). Masum görünen `dotnet build`, tek bir MSBuild özelliğiyle bir shell'e dönüşebiliyor. Tester'ın dotnet girişlerinin prefix değil sabit token listesi olmasının nedeni bu. Bu blok sonuç ne çıkarsa çıksın zaten planda vardı; C2.6 riskin gerçek olduğunu kaydetti.
4. **Yanlış iç içe yazılmış YAML sessizce hiçbir hook kurmayabilir.** Frontmatter'daki `hooks:` bloğu yanlış girintilense bile geçerli bir YAML olarak okunabilir; Claude Code da uyarı vermeden ajanı hook'suz çalıştırır. Bu yüzden her bağlantı, adı geçen hook'un o ajan ve o çağrı için yazdığı bir log satırıyla kanıtlanıyor (C2.2). Statik dosya tek başına yeterli sayılmıyor.
5. **Model reddi hook bloğu sayılmaz.** Coder'ın 2a–2d'yi reddetmesi hiçbir araç çağrısı üretmedi, yani hook hiç çalışmadı. Bu hedefler "geçti" diye işaretlenmedi; "sonuç belirsiz" olarak kaldı. Revision 8'e kadar plan, bu hedefleri yeniden istemeyi bir kapanış koşulu olarak tutuyordu.
6. **Kardeş dizinler ve `..`.** `startsWith(scope)` tek başına kullanılırsa `reviews-evil` dizinini de kabul eder. `..` parçaları çözülmeden karşılaştırma yapılırsa da `reviews/../plans` kapsamın içindeymiş gibi görünür. M2.3 ve M2.4 bu iki gerilemenin testlerce yakalandığını gösterdi.
7. **`disableBundledSkills` bir yetki sınırı değil ve P2.8 etkisini gösteremiyor.** H2.3 baz kaydında da hiç bundled skill görünmüyordu. Dolayısıyla P2.8'deki "bundled skill yok" gözlemi, ayarın bir şeyi kaldırdığını kanıtlamaz. Ayrıca `.claude/settings.json` dosyasını `path-guard` korumuyor. Bu madde Faz 4'ün artık risk listesine gidecek.
8. **Çalışan modeli frontmatter'dan değil transcript'ten okumak.** Frontmatter'da yazan model, gerçekten çalışan modelin kanıtı değil. Tester'ın devam çalıştırmasının transcript'i `claude-sonnet-5-5` ve effort `low` gösteriyor. Bu bilgi bir mekanik kayıttan geliyor; `/tasks` ekranında görülen ise bir insan gözlemi.

## Farklı düşündüğüm yer

- **`--output` kontrolü ham token'a bakıyor.** İlk ön kod incelemesinin F2 bulgusu bunu gösterdi. `git diff "--output=src/a.cs"` hook'tan geçer, çünkü token `"` ile başlıyor. Ardından shell tırnakları kaldırır ve git `--output=src/a.cs` argümanını alır. Git'in bu argümanla dosya yazıp yazmadığı ise DOĞRULANMAMIŞ. İnceleme bunu "string sezgileri" artık riskine koydu ve engelleyici saymadı. Ben kontrolden önce token'ın başındaki ve sonundaki tırnakları soyardım (coder-bash-guard'da `stripQuotes` zaten var) ve buna bir test eklerdim. Bir satırlık düzeltmesi olan bir açığı artık risk olarak kaydetmek, işbirlikçi ajan tehdit modelinde bile gereksiz bir açık bırakıyor.
- **Doğrulama düzeneği kodun kendisinden ağır.** Dalda yaklaşık 2.260 satır hook kodu ve testi var. Plan, öneri, inceleme ve araştırma dosyaları ise yaklaşık 10.900 satır tutuyor; bunlara git'e girmeyen 18.000 satırı aşan bir kanıt dosyası ekleniyor. Plan on revizyon geçirdi. Kazandığımız: model raporlarındaki yanlış iddialar mekanik kayıtlarla yakalandı. Verdiğimiz: zaman. Plan 2026-09-25'te başladı; bu kayıt 2026-10-12'de yazılıyor ve Faz 3–4 hâlâ başlamadı. Bu maliyetin önemli bir kısmı, P2.2 ve P2.4'ün ajanlardan kendi rol kurallarıyla çelişen işlemler istemesinden doğdu. Revision 8–10 bunu sonradan düzeltti. Ben Faz 2'nin canlı negatif problarını baştan "rolüyle çelişen bir şey istenmez" ilkesiyle tasarlar, bu çatışmayı planlama aşamasında kapatırdım.
- **Günlük numaralandırması artık PR numaralandırmasından ayrıldı.** `explainer.md` hâlâ "NNNN matches the PR number" diyor. D3 bu kuralı bu kayıt için bilerek bozdu, ama kuralın metni değişmedi. Bu yüzden bir sonraki explainer çalıştırması çelişkili bir talimatla karşılaşacak. Ben numaralandırma kuralını aynı kararla birlikte güncellerdim. Bu kayıt da çatışmayı başlığa "PR" yazmayarak çözmek zorunda kaldı.

## Kendini sına

1. `bash-allowlist.js`'i tester profiliyle ve `dotnet test --filter -v` girdisiyle satır satır izle. Komut hangi fonksiyonda, hangi satırda ve hangi sebep metniyle bloklanır? Kod `-v`/`--verbosity` dalına neden hiç ulaşmaz?
2. Coder `git -C src status` çalıştırmak istiyor. `coder-bash-guard.js`'teki `checkGitCall` bu komut için ne karar verir ve oraya hangi satırlardan geçerek varır? Aynı komut `.claude/settings.json`'daki izin kurallarına göre ne olur? İki katmanın farklı karar vermesi bir tutarsızlık mı, yoksa savunulabilir bir tasarım mı? Gerekçelendir.
3. İki Bash hook'u hedefi `runGuard(<ad>, decide, getCommand)` çağrısıyla, üçüncü parametre olarak alıyor. Alternatif, `getTargetPath` içine `?? tool_input.command` eklemekti. Neden extractor parametresi seçildi? Bir ajan dosyasında `write-scope.js` yanlışlıkla `matcher: "Bash"` altına bağlansaydı, iki tasarımda ayrı ayrı ne olurdu?
4. M2.8 mutasyonu (dotnet girişinden sonraki her token'ı kabul etmek) dokuz blok testini kırmızıya çevirdi. Ama `bash-allowlist: allows dotnet test -v minimal` gibi dotnet izin testleri yeşil kaldı. Neden? Bunun tersi bir hata olsaydı, örneğin `-warnaserror` `DOTNET_FLAGS`'ten düşseydi, 216 testten hangisi ya da hangileri bunu yakalardı? Aynı adı taşıyan `coder-bash-guard` testi neden bu listede yer almaz?
5. `write-scope.js`'teki 15–17. satırlar silinseydi, `write-scope.test.js`'teki 17 testten hangileri kırmızıya dönerdi? Hangi kapsam argümanıyla yapılandırılmış bir ajan bu durumda projedeki her yola yazma izni alırdı?
6. M2.1 ve M2.2 canlı `.claude/hooks/coder-bash-guard.js` dosyası üzerinde, birer coder turunda uygulandı. M2.3–M2.8 ise `ce5e7cf`'ten repo dışına çıkarılan atılabilir kopyalarda çalıştırıldı (Revision 7). İkinci yöntem neler kazandırdı, neler kaybettirdi? Kopya yönteminin hiçbir koşulda gösteremeyeceği bir şey var mı?
7. Bu kaydı yazan explainer, P2.5'in `git log --oneline -3 --grep=LIVEPROBE-5e` işlemini tek bir Bash çağrısıyla yaptı. İnsan bu çağrıyı `TestResults/hook-log/hooks.jsonl` ile eşleştirirken neden `LIVEPROBE-5e` etiketini log'un tamamında saymakla yetinmiyor? `logDecision`'ın yazdığı alanlardan hangileri bu eşleştirmeyi mümkün kılıyor? Hook girdisinde `agent_id` hiç olmasaydı, bu bağlantının kanıt değerine ne olurdu?
