const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const Amazon_articles_models = sequelize.define("artykuly_amazon", { 
    Title: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Link: {
        type: DataTypes.STRING(1000),
        allowNull: true
    },
    Image : {
        type: DataTypes.STRING(500),
        allowNull: true
    },
    Rating : {
        type: DataTypes.STRING(500),
        allowNull: true
    },
    RatingInStars: {
        type: DataTypes.STRING(500),
        allowNull: true
    },
    Delivery : {
        type: DataTypes.STRING(500),
        allowNull: true
    },
    FreeDelivery : {
        type: DataTypes.STRING(500),
        allowNull: true
    },
    OriginalPrice : {
        type: DataTypes.STRING(500),
        allowNull: true
    },
    PromoPrice : {
        type: DataTypes.STRING(500),
        allowNull: true
    },
    PriceWithoutCurrency : {
        type: DataTypes.STRING(500),
        allowNull: true
    },
    CommentsNumber: {
        type: DataTypes.STRING(500),
        allowNull: true
    }
},{
    tableName: 'amazon',
    timestamps: false
});

module.exports=Amazon_articles_models;