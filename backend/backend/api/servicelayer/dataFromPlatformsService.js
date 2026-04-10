const dataFromPlatformsDal=require('../dataaccesslayer/dataFromPlatformsDal');

const getPepperArticles = async (numberLimit)=>{
    
    const PepperArticles= await dataFromPlatformsDal.getPepperArticles(numberLimit);
    return (PepperArticles);
}

const getOlxArticles = async (numberLimit)=>{
    
    const OlxArticles= await dataFromPlatformsDal.getOlxArticles(numberLimit);
    return (OlxArticles);
}

const getAmazonArticles = async (numberLimit)=>{
    
    const AmazonArticles= await dataFromPlatformsDal.getAmazonArticles(numberLimit);
    return (AmazonArticles);
}

const getAllegroArticles = async (numberLimit)=>{
    
    const AllegroArticles= await dataFromPlatformsDal.getAllegroArticles(numberLimit);
    return (AllegroArticles);
}

const getOtoMotoArticles = async (numberLimit)=>{
    
    const OtoMotoArticles= await dataFromPlatformsDal.getOtoMotoArticles(numberLimit);
    return (OtoMotoArticles);
}

const getOtoDomArticles = async (numberLimit)=>{
    
    const OtoDomArticles= await dataFromPlatformsDal.getOtoDomArticles(numberLimit);
    return (OtoDomArticles);
}

const getAutoscoutArticles = async (numberLimit)=>{
    
    const AutoscoutArticles= await dataFromPlatformsDal.getAutoscoutArticles(numberLimit);
    return (AutoscoutArticles);
}

const getGratkaArticles = async (numberLimit)=>{
    
    const GratkaArticles= await dataFromPlatformsDal.getGratkaArticles(numberLimit);
    return (GratkaArticles);
}

const getSprzedajemyArticles = async (numberLimit)=>{
    
    const SprzedajemyArticles= await dataFromPlatformsDal.getSprzedajemyArticles(numberLimit);
    return (SprzedajemyArticles);
}

const getAutocentrumArticles = async (numberLimit)=>{
    
    const AutocentrumArticles= await dataFromPlatformsDal.getAutocentrumArticles(numberLimit);
    return (AutocentrumArticles);
}

module.exports={getPepperArticles, getOlxArticles, getAmazonArticles, getAllegroArticles, getOtoMotoArticles,getOtoDomArticles, getAutoscoutArticles, getGratkaArticles, getSprzedajemyArticles, getAutocentrumArticles};
