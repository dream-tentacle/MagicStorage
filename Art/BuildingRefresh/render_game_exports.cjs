const fs=require('fs');const path=require('path');
const sharp=require('C:/Users/x1353/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dir=path.join(__dirname,'GameExport');
const manifest=JSON.parse(fs.readFileSync(path.join(dir,'manifest.json'),'utf8'));
(async()=>{
  for(const item of manifest.renders){
    const target=path.join(dir,item.png);fs.mkdirSync(path.dirname(target),{recursive:true});
    await sharp(path.join(dir,item.svg)).png().toFile(target);
  }
  console.log(`Rendered ${manifest.renders.length} game SVGs at final texture resolution.`);
})().catch(err=>{console.error(err);process.exit(1)});
