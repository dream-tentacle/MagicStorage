const fs=require('fs');
const path=require('path');
const sharp=require('C:/Users/x1353/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dir=path.join(__dirname,'Directions');
const records=JSON.parse(fs.readFileSync(path.join(dir,'manifest.json'),'utf8'));
(async()=>{
  for(const item of records) await sharp(path.join(dir,item.svg)).png().toFile(path.join(dir,item.png));
  console.log(`Rendered ${records.length} direction PNGs.`);
})().catch(error=>{console.error(error);process.exit(1)});
