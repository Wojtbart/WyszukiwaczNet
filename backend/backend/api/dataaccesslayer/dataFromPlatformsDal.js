const Pepper_model=require('../models/Pepper_articles');
const OlxModel=require('../models/OlxModel');
const Allegro_model=require('../models/Allegro_articles');
const Amazon_model=require('../models/Amazon_articles');
const OtoMotoModel=require('../models/OtoMotoModel');
const OtoDomModel=require('../models/OtoDomModel');
const AutoscoutModel = require('../models/AutoscoutModel');
const GratkaModel = require('../models/GratkaModel');
const SprzedajemyModel = require('../models/SprzedajemyModel');
const AutocentrumModel = require('../models/AutocentrumModel');

const getPepperArticles = async (numberLimit)=>{
    let pepperArticles={};

    try {
        pepperArticles= await Pepper_model.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })

    } catch (err) {
       console.error(err);
    }

    return pepperArticles;
}

const getOlxArticles = async (numberLimit)=>{
    let olxArticles={};

    try {
        //pobieram dane z najnowszym id w tabeli czyli od konca
        olxArticles= await OlxModel.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })

    } catch (err) {
       console.error(err);
    }

    return olxArticles;
}

const getAmazonArticles = async (numberLimit)=>{
    let amazonArticles={};

    try {
        amazonArticles= await Amazon_model.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })

    } catch (err) {
       console.error(err);
    }

    return amazonArticles;
}

const getAllegroArticles = async (numberLimit)=>{
    let allegroArticles={};

    try {
        allegroArticles= await Allegro_model.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })

    } catch (err) {
       console.error(err);
    }

    return allegroArticles;
}

const getOtoMotoArticles = async (numberLimit)=>{
    let otoMotoArticles={};

    try 
    {
        otoMotoArticles= await OtoMotoModel.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })
    } 
    catch (err) 
    {
       console.error(err);
    }

    return otoMotoArticles;
}

const getOtoDomArticles = 
async (numberLimit)=>{
    let otoDomArticles={};

    try 
    {
        otoDomArticles= await OtoDomModel.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })
    } 
    catch (err) 
    {
       console.error(err);
    }

    return otoDomArticles;
}

const getAutoscoutArticles = async (numberLimit)=>{
    let autoscoutArticles={};

    try {
        autoscoutArticles= await AutoscoutModel.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })

    } catch (err) {
       console.error(err);
    }

    return autoscoutArticles;
}

const getGratkaArticles = async (numberLimit)=>{
    let gratkaArticles={};

    try {
        gratkaArticles= await GratkaModel.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })

    } catch (err) {
       console.error(err);
    }

    return gratkaArticles;
}

const getSprzedajemyArticles = async (numberLimit)=>{
    let sprzedajemyArticles={};

    try {
        sprzedajemyArticles= await SprzedajemyModel.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })

    } catch (err) {
       console.error(err);
    }

    return sprzedajemyArticles;
}
const getAutocentrumArticles = async (numberLimit)=>{
    let autocentrumArticles={};

    try {
        autocentrumArticles= await AutocentrumModel.findAll({
            order: [["id","DESC"]], 
            limit: parseInt(numberLimit)
        })

    } catch (err) {
       console.error(err);
    }

    return autocentrumArticles;
}

module.exports={getPepperArticles, getOlxArticles, getAmazonArticles, getAllegroArticles, getOtoMotoArticles,getOtoDomArticles, getAutoscoutArticles, getGratkaArticles, getSprzedajemyArticles, getAutocentrumArticles};