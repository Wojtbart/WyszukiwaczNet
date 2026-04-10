const dataFromPlatformsService = require('../servicelayer/dataFromPlatformsService');
const { spawn } = require('child_process');

const REGEX = /(?<=.*: )\d+/;
const PYTHON = process.env.PYTHON || 'python'; // const PYTHON='python3' for LINUX

const getDataFromPlatform = async (req, website, script, serviceMethod) => {
    const { phrase, additional_phrase, request_number } = req.body;

    let numberReturnedRecordsFromBackend = 0;
    let numberLimit = 0;

    let final_phrase = phrase + " " + additional_phrase

    console.log("additional_phrase", additional_phrase)
    
    const args = [`../${script}`, final_phrase];
    const python = spawn(PYTHON, args);

    return new Promise((resolve, reject) => {
        python.stdout.on('data', (data) => {
            console.log(`Data from ${website}:`, data.toString());
            numberReturnedRecordsFromBackend = parseInt(data.toString().match(REGEX)?.[0] || '0');
        });

        python.stderr.on('data', (data) => {
            console.error(`Error executing script for ${website}:`, data.toString());
        });

        python.on('close', (code) => {
            if (code === 0) {
                console.log(`Process for ${website} finished successfully with code ${code}`);
            } else {
                console.error(`Process for ${website} finished with error code ${code}`);
            }
        });

        python.on('exit', async () => {
            numberLimit = Math.min(Number(request_number) || 0, numberReturnedRecordsFromBackend || 0);

            try {
                const articles = await dataFromPlatformsService[serviceMethod](numberLimit);
                resolve(articles);
            } catch (error) {
                reject(error);
            }
        });
    });
};

const getDataFromWebsite = async (req, res) => {
    try {
        const { websites, additional_phrase, phrase, request_number } = req.body;

        if (!websites || !Array.isArray(websites)) {
            return res.status(400).json({
                success: false,
                message: "'websites' must be an array."
            });
        }
        
        if (!phrase || typeof phrase !== 'string' || phrase.trim() === '') {
            return res.status(400).json({
                success: false,
                message: "'phrase' is required and cannot be empty."
            });
        }
        if ( typeof additional_phrase !== 'string') {
            return res.status(400).json({
                success: false,
                message: "'additional_phrase' must be empty or must have value."
            });
        }

        if (!request_number || isNaN(request_number) || request_number <= 0) {
            return res.status(400).json({
                success: false,
                message: "'request_number' is required and must be a number greater than 0."
            });
        }

        const websiteData = {
            pepper: { script: 'pepper.py', serviceMethod: 'getPepperArticles' },
            olx: { script: 'olx_scrapper.py', serviceMethod: 'getOlxArticles' },
            allegro: { script: 'allegro_scraper.py', serviceMethod: 'getAllegroArticles' },
            amazon: { script: 'amazon_scrapper.py', serviceMethod: 'getAmazonArticles' },
            otoMoto: { script: 'oto_moto_scrapper.py', serviceMethod: 'getOtoMotoArticles' },
            otoDom: { script: 'oto_dom_scrapper.py', serviceMethod: 'getOtoDomArticles' },
            autoscout: { script: 'autoscout_scrapper.py', serviceMethod: 'getAutoscoutArticles' },
            gratka: { script: 'gratka_scrapper.py', serviceMethod: 'getGratkaArticles' },
            sprzedajemy: { script: 'sprzedajemy_scrapper.py', serviceMethod: 'getSprzedajemyArticles' },
            autocentrum: { script: 'autocentrum_scrapper.py', serviceMethod: 'getAutocentrumArticles' },
        };

        let dataResults = {};

        for (const website of websites) { 
            if (!websiteData[website]) {
                throw new Error(`Unknown website: ${website}`);
            }
            const { script, serviceMethod } = websiteData[website];
            dataResults[`${website}Data`] = await getDataFromPlatform(req, website, script, serviceMethod); //UWAGA dodajemy Data na końcu do elementu
        }

        res.status(201).json({
            success: true,
            message: 'Data retrieved successfully!',
            ...dataResults,
        });
        console.log('Server successfully retrieved data from platforms!');

    } catch (err) {
        console.error('Error fetching data:', err);

        res.status(500).send({
            success: false,
            message: 'An error occurred during data retrieval!',
            error: err.message
        });
    }
};

module.exports = { getDataFromWebsite };
