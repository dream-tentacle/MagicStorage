const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));
async function main() {
  for(const [name,width,height] of [['StorageCore-v16',512,512],['StorageCore-ring-v16',512,282]]) {
    const stem=path.join(__dirname,name);
    await sharp(fs.readFileSync(stem+'.svg'),{density:192})
      .resize(width,height).png().toFile(stem+'.png');
    console.log(stem+'.png');
  }
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
