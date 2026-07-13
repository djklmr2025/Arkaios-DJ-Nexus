const express = require('express');
const cors = require('cors');
const fs = require('fs');
const path = require('path');

const app = express();
app.use(cors());
app.use(express.json());

const dbPath = path.join(__dirname, 'licenses.json');

// Initialize DB if not exists
if (!fs.existsSync(dbPath)) {
    fs.writeFileSync(dbPath, JSON.stringify({}));
}

// Endpoint to add a license (Called by Keygen)
app.post('/api/licenses/add', (req, res) => {
    try {
        const { key, hwid, type, name, phone } = req.body;
        
        let licenses = JSON.parse(fs.readFileSync(dbPath, 'utf8'));
        
        licenses[key] = {
            hwid,
            type,
            name,
            phone,
            status: "ACTIVE",
            createdAt: new Date().toISOString()
        };
        
        fs.writeFileSync(dbPath, JSON.stringify(licenses, null, 4));
        
        console.log(`[+] Nueva licencia registrada en la Nube: ${name}`);
        res.json({ success: true, message: "License registered." });
    } catch (err) {
        console.error(err);
        res.status(500).json({ success: false, message: "Server error" });
    }
});

// Endpoint to validate a license (Called by DJ Assistant)
app.post('/api/licenses/validate', (req, res) => {
    try {
        const { key, hwid } = req.body;
        let licenses = JSON.parse(fs.readFileSync(dbPath, 'utf8'));
        
        if (licenses[key]) {
            const lic = licenses[key];
            
            if (lic.status === "REVOKED") {
                console.log(`[!] Bloqueo: Licencia revocada intentó acceso (${lic.name})`);
                return res.status(403).json({ success: false, message: "License REVOKED." });
            }
            
            if (lic.type === "BASIC" && lic.hwid !== hwid) {
                console.log(`[!] Bloqueo: HWID no coincide para licencia de ${lic.name}`);
                return res.status(403).json({ success: false, message: "HWID mismatch." });
            }
            
            console.log(`[OK] Acceso concedido en la Nube: ${lic.name}`);
            return res.json({ 
                success: true, 
                message: "Valid license.",
                latestVersion: "v1.0.0",
                updateUrl: "https://github.com/djklmr2025/Arkaios-DJ-Nexus/releases"
            });
        } else {
            console.log(`[?] Bloqueo: Llave no registrada en la Nube intentó acceso.`);
            return res.status(404).json({ success: false, message: "License not found." });
        }
    } catch (err) {
        console.error(err);
        res.status(500).json({ success: false, message: "Server error" });
    }
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
    console.log(`Arkaios Cloud API (Node.js) corriendo en puerto ${PORT}`);
});
