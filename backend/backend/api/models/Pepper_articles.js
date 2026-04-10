const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const Pepper_articles_models = sequelize.define("artykuly_pepper", { 
    Tytul: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Link: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Cena_oryginalna: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Obnizka_w_procentach: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Cena_promocyjna: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Dostawa: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Zdjecie: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Opis: {
        type: DataTypes.STRING,
        allowNull: true
    },
    uzytkownik_wystawiajacy: {
        type: DataTypes.STRING,
        allowNull: true
    },
    ilosc_komentarzy: {
        type: DataTypes.STRING,
        allowNull: true
    },
    ilosc_glosow_za_produktem: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Czy_promocja_trwa: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Opublikowano: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Kupony_promocyjne: {
        type: DataTypes.STRING,
        allowNull: true
    },
    Firma_sprzedajaca: {
        type: DataTypes.STRING,
        allowNull: true
    },
    avatar: {
        type: DataTypes.STRING,
        allowNull: true
    },
},{
    tableName: 'artykuly_pepper',
    timestamps: false
});

module.exports=Pepper_articles_models;