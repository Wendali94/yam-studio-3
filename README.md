# YAM Studio Manager

የ YAM PICTURE ስቱዲዮ Windows ሶፍትዌር። ቀጠሮ ← ካሜራ መቀበል ← የደንበኛ ምርጫ ← ኤዲቲንግ ← ክፍያ ← ርክክብ።
ሁሉም ቀን እና ሰዓት የኢትዮጵያ ነው፤ ቋንቋ (አማርኛ/English) በአንድ ቦታ ይቀየራል። ኢንተርኔት አያስፈልግም።

## 1. GitHub ላይ መስራት (installer ማውጣት)

1. GitHub ላይ አዲስ repository ፍጠር (ለምሳሌ `yam-studio`)።
2. ከዚህ zip ውስጥ **ያሉትን ሁሉ ፋይሎች እና folders** (`.github` ጨምሮ) repository ውስጥ ጫን፤ `YamStudio.csproj` repository ሥር (root) ላይ መሆን አለበት።
3. **Actions** tab → **Build YAM Studio** → **Run workflow** (ወይም ፋይል ሲጫን በራሱ ይጀምራል)።
4. ሲጨርስ (አረንጓዴ ✔) ገጹ ታች **Artifacts** ውስጥ **YamStudio-Setup** አውርድ → ውስጡ `YamStudio-Setup.exe` አለ።
5. ቀይ ✖ ከሆነ፦ የ Actions log ውስጥ ያለውን የስህተት መስመር ኮፒ አድርገህ ላክልኝ፤ አስተካክላለሁ (ኮዱን እኔ አልተኮመፓይል አላደረግኩትም፤ በመጀመሪያ ሙከራ ጥቂት ስህተቶች ሊወጡ ይችላሉ)።

## 2. መጫን

- `YamStudio-Setup.exe` ን በ **ሁለቱም PC** ላይ አሂድ (Windows 10/11 64-bit)። .NET መጫን አያስፈልግም።
- Installer ለካሜራ እና ለ PC2 የ Windows Firewall ፍቃድ በራሱ ይጨምራል።

## 3. የመጀመሪያ ማዋቀር

**PC1 (ዋና):** "PC1" ምረጥ → ዳታ እና ፎቶ የሚቀመጥበት folder (ትልቅ ዲስክ፣ ለምሳሌ `D:\YamStudio`)።
ከዚያ **ቅንብር** ገጽ ላይ **IP** እና **PIN** ይታያል።

**PC2 (ኤዲቲንግ):** "PC2" ምረጥ → የ PC1 IP እና PIN አስገባ → ጀምር።

ሁለቱም ኮምፒውተር በአንድ router (ወይም LAN ገመድ) ላይ መሆን አለባቸው። PC1 IP እንዳይቀየር በ router ውስጥ ለ PC1 ቋሚ IP (DHCP reservation) ስጠው።

## 4. Sony a7 IV ማገናኘት

PC1 ላይ **ከካሜራ መቀበል** ገጽ የ IP፣ Port፣ ስም እና የይለፍ ቃል ያሳያል።
ካሜራ ላይ (የሜኑ ስሞች በ firmware ሊለያዩ ይችላሉ)፦

1. Menu → Network → Wi-Fi/Access Point → ከስቱዲዮ router ጋር አገናኝ (ካሜራው እና PC1 አንድ network ላይ)።
2. Menu → Network → Transfer/Remote → **FTP Transfer Func.** → FTP Function **On**።
3. **Server Setting** → Server 1 → Hostname = PC1 IP · Port = `2121` · User/Password = በገጹ ላይ ያሉት · **Secure connection Off** · **Passive mode On**።
4. **Auto Img. Transfer** ወይም ፎቶ በእጅ መላክ ምረጥ። RAW+JPEG ሲልክ ሶፍትዌሩ ሁለቱን እንደ አንድ ፎቶ ያጣምራል (ማሳያው ከ JPEG ወይም ከ RAW ውስጥ ካለው preview)።

ካሜራው ካልሰራ ምትክ፦ **ከ folder / SD ካርድ ጨምር** ቁልፍ ወይም ተጨማሪ የሚከታተል folder (ቅንብር)።

## 5. የዕለት ስራ

1. **ምዝገባ እና ቀጠሮ** — ስም፣ ስልክ፣ ቀን/ሰዓት (ኢትዮጵያ)፣ ዋጋ፣ ቅድመ ክፍያ።
2. ጠዋት **የዛሬ ስራ** ይከፈታል → ደንበኛ ሲመጣ **📷 ካሜራ መቀበል** → ፎቶ ሲነሳ በራሱ ይደርሳል።
3. **❤ ምርጫ** → **የደንበኛ ሁነታ** (ሙሉ ስክሪን፣ ← → እና Space) → **ምርጫ አረጋግጥ**።
   `ስም_ስልክ_ቀን` folder ይፈጠራል፦ `Originals / Selected / Edited / Final`።
4. **PC2** ላይ "ስራዎች" → **ኤዲቲንግ ጀምር** → **ፎቶ አውርድ** → አስተካክል → **የተጠናቀቁ ላክ** → **ለማረጋገጫ ላክ**።
5. **PC1** ላይ **አጽድቅ እና አስረክብ**፤ **ወደ USB/HDD ቅዳ** ለደንበኛ።
6. **ክፍያ እና ሪፖርት** — ክፍያ፣ ደረሰኝ ማተም፣ ወርሃዊ (የኢትዮጵያ ወር) ሪፖርት እና CSV።

## 6. Backup

ቅንብር → Backup ቦታ (ሁለተኛ ዲስክ ወይም በ PC2 የተጋራ folder) → በየምሽቱ እና ሲዘጋ በራስ-ሰር። አዲስ ፋይሎች ብቻ ይቀዳሉ።

## 7. የመጀመሪያ ሙከራ ዝርዝር (እባክህ በዚህ ቅደም ተከተል)

- [ ] ፕሮግራሙ ይከፈታል፣ ቀኑ ዛሬን በትክክል (ጥቅምት/ኅዳር ...) ያሳያል
- [ ] አዲስ ቀጠሮ ማስቀመጥ፣ በ "የዛሬ ስራ" ይታያል
- [ ] "ከ folder ጨምር" ከ 5 ፎቶ (JPG) ጋር → ምርጫ → folder ይፈጠራል
- [ ] ተመሳሳዩን በ Sony **ARW** ፋይሎች ሞክር (ማሳያው ይታያል?)
- [ ] ካሜራ ከ FTP ጋር፦ አንድ ፎቶ አንሳ → ይደርሳል?
- [ ] PC2 ተገናኝቶ ስራውን ያያል፣ ፎቶ ያወርዳል፣ ይልካል
- [ ] Backup ወደ ሁለተኛ ዲስክ

## ገደቦች (በግልጽ)

- ኮዱ በ GitHub ላይ የመጀመሪያ ጊዜ ነው የሚገነባው፤ ከ Sony a7 IV እና ከሁለት PC ጋር በእውነት አልተሞከረም።
- ከ ARW ውስጥ ያለውን preview ማውጣት በራስ ስልተ ቀመር ነው፤ በሁሉም firmware ላይ ማረጋገጥ ያስፈልጋል።
- ፎቶ ኤዲቲንግ በ Lightroom/Photoshop ነው የሚሰራው፤ ይህ ሶፍትዌር ፋይሎችን ያደራጃል እና ሁኔታ ይከታተላል።
- አንድ ዳታ ብቻ (PC1) አለ፤ PC1 ካልበራ PC2 አይሰራም፤ ስለዚህ backup አስፈላጊ ነው።
